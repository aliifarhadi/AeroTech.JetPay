using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.AcceptanceTests.Fakes;
using AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CancelPaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ExpireDuePayments;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.VerifyPaymentIntent;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Queries.GetPaymentSessionById;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.JetPay.IntegrationEvents.V1;
using AeroTech.Messages.Shared.Enums;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fixtures;

public sealed class PaymentHarness : IDisposable
{
    public const int Irr = 70;
    public const int Usd = 840;
    public const long CustomerId = 7001;
    public const long AgencyId = 8001;
    public const long CorporateId = 9001;
    public const long CashOfficeId = 501;
    public const long EmployeeId = 42;
    public const decimal Amount = 1_000_000m;

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly HashSet<TenderType> _crashBeforeDispatch = [];
    private int _keys;

    public PaymentHarness(bool allowPgwForStaffAssisted = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentSessions:LockExpirySeconds"] = "30",
                ["PaymentAcceptancePolicy:AllowPgwForStaffAssisted"] = allowPgwForStaffAssisted.ToString(),
                ["MockJetPay:Enabled"] = "true",
                ["MockJetPay:PublicBaseUrl"] = "http://mock.jetpay.test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(configuration);
        services.AddMockJetPay(configuration);
        WrapTenderProvidersForCrashes(services);

        services.Replace(ServiceDescriptor.Singleton<IClock>(Clock));
        services.AddSingleton<IIdGenerator>(Ids);
        services.AddSingleton<IDistributedLock>(Lock);
        services.AddSingleton<IOutboxWriter>(Outbox);
        services.AddScoped<InMemoryUnitOfWork>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<InMemoryUnitOfWork>());
        services.AddScoped<InMemoryPaymentSessionRepository>();
        services.AddScoped<IPaymentSessionRepository>(provider => provider.GetRequiredService<InMemoryPaymentSessionRepository>());
        services.AddScoped<InMemoryPaymentIntentRepository>();
        services.AddScoped<IPaymentIntentRepository>(provider => provider.GetRequiredService<InMemoryPaymentIntentRepository>());
        services.AddScoped<InMemoryIdempotencyRecordRepository>();
        services.AddScoped<IIdempotencyRecordRepository>(provider => provider.GetRequiredService<InMemoryIdempotencyRecordRepository>());

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateScope();
    }

    public FixedClock Clock { get; } = new();

    public SequentialIdGenerator Ids { get; } = new();

    public InMemoryDistributedLock Lock { get; } = new();

    public RecordingOutboxWriter Outbox { get; } = new();

    public MockFundingLedger Ledger => _provider.GetRequiredService<MockFundingLedger>();

    public InMemoryPaymentSessionRepository Sessions => _scope.ServiceProvider.GetRequiredService<InMemoryPaymentSessionRepository>();

    public InMemoryPaymentIntentRepository Intents => _scope.ServiceProvider.GetRequiredService<InMemoryPaymentIntentRepository>();

    public static PaymentInitiatorContextView Web => new(SalesChannel.IBE, "Customer", CustomerId, null);

    public static PaymentInitiatorContextView AgencyApi => new(SalesChannel.PartnerAPI, "AgencyApiPrincipal", 88, null);

    public static PaymentInitiatorContextView BackOffice(long officeId = CashOfficeId) => new(SalesChannel.BackOffice, "AirlineEmployee", EmployeeId, officeId);

    public MockWallet Wallet(PayerType payerType, long payerId, decimal balance, bool isDefault = false, int currencyId = Irr, string code = "main")
        => Ledger.SetWallet(payerType, payerId, currencyId, code, balance, isDefault);

    public MockCreditFacility Credit(TenderType tenderType, PayerType payerType, long payerId, decimal limit, int? validitySeconds = null, int currencyId = Irr)
        => Ledger.SetCreditFacility(tenderType, payerType, payerId, currencyId, limit, validitySeconds);

    public void AcceptCashAt(long officeId = CashOfficeId) => Ledger.SetCashAcceptance([officeId], []);

    public void CrashBeforeNextDispatchOf(TenderType tenderType) => _crashBeforeDispatch.Add(tenderType);

    public string NewKey() => $"idem-{Interlocked.Increment(ref _keys)}";

    public Task<T> SendAsync<T>(IRequest<T> request) => _scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);

    public static CreatePaymentSessionCommand CreateCommand(
        string idempotencyKey,
        PayerType payerType = PayerType.Customer,
        long payerId = CustomerId,
        PaymentInteractionMode interactionMode = PaymentInteractionMode.CustomerInteractive,
        PaymentAssuranceRequirement assurance = PaymentAssuranceRequirement.IssuanceGuaranteed,
        PaymentInitiatorContextView? initiator = null,
        decimal amount = Amount,
        int currencyId = Irr,
        long orderId = 5001,
        int commercialVersion = 1,
        DateTimeOffset? expiresAt = null,
        string? payableInstructionId = null)
        => new(
            idempotencyKey,
            payableInstructionId ?? $"payable-{orderId}-v{commercialVersion}",
            orderId,
            $"ORD-{orderId}",
            commercialVersion,
            PaymentPurpose.InitialSale,
            payerType,
            payerId,
            initiator ?? DefaultInitiator(interactionMode),
            amount,
            currencyId,
            assurance,
            interactionMode,
            expiresAt);

    public Task<PaymentSessionResponse> CreateAsync(
        PayerType payerType = PayerType.Customer,
        long payerId = CustomerId,
        PaymentInteractionMode interactionMode = PaymentInteractionMode.CustomerInteractive,
        PaymentAssuranceRequirement assurance = PaymentAssuranceRequirement.IssuanceGuaranteed,
        PaymentInitiatorContextView? initiator = null,
        decimal amount = Amount,
        int currencyId = Irr,
        long orderId = 5001,
        int commercialVersion = 1,
        DateTimeOffset? expiresAt = null)
        => SendAsync(CreateCommand(NewKey(), payerType, payerId, interactionMode, assurance, initiator, amount, currencyId, orderId, commercialVersion, expiresAt));

    public Task<IReadOnlyList<PaymentMethodOptionView>> OptionsFor(PaymentSessionView session, decimal? amount = null)
        => SendAsync(new ResolvePaymentMethodOptionsQuery(
            session.OrderId,
            session.OrderReference,
            session.CommercialVersion,
            session.Purpose,
            session.PayerType,
            session.PayerId,
            session.InitiatorContext,
            amount ?? session.RequiredAmount,
            session.CurrencyId,
            session.AssuranceRequirement,
            session.InteractionMode));

    public Task<IReadOnlyList<PaymentMethodOptionView>> ResolveAsync(
        PayerType payerType,
        long payerId,
        PaymentInteractionMode interactionMode,
        PaymentAssuranceRequirement assurance = PaymentAssuranceRequirement.IssuanceGuaranteed,
        PaymentInitiatorContextView? initiator = null,
        decimal amount = Amount,
        int currencyId = Irr)
        => SendAsync(new ResolvePaymentMethodOptionsQuery(
            5001,
            "ORD-5001",
            1,
            PaymentPurpose.InitialSale,
            payerType,
            payerId,
            initiator ?? DefaultInitiator(interactionMode),
            amount,
            currencyId,
            assurance,
            interactionMode));

    public static string OptionOf(IReadOnlyList<PaymentMethodOptionView> options, TenderType tenderType)
        => options.Single(option => option.TenderType == tenderType).Id;

    public Task<PaymentSessionResponse> ConfirmAsync(string sessionId, IReadOnlyList<PaymentSelection> selections, string? idempotencyKey = null, string? returnUrl = null)
        => SendAsync(new ConfirmPaymentSessionCommand(idempotencyKey ?? NewKey(), sessionId, PaymentSelectionMode.Explicit, selections, returnUrl));

    public async Task<PaymentSessionResponse> FundAsync(PaymentSessionView session, string? idempotencyKey, params (TenderType Tender, decimal Amount)[] legs)
    {
        var options = await OptionsFor(session, legs.Sum(leg => leg.Amount));
        return await ConfirmAsync(session.Id, legs.Select(leg => new PaymentSelection(OptionOf(options, leg.Tender), leg.Amount)).ToList(), idempotencyKey);
    }

    public Task<PaymentSessionResponse> ConfirmDefaultAsync(string sessionId, string? idempotencyKey = null)
        => SendAsync(new ConfirmPaymentSessionCommand(idempotencyKey ?? NewKey(), sessionId, PaymentSelectionMode.Default, [], null));

    public Task<PaymentSessionResponse> CompleteAsync(string paymentIntentId, MockCustomerOutcome outcome = MockCustomerOutcome.Approved)
    {
        Ledger.SetCustomerOutcome(paymentIntentId, outcome);
        return SendAsync(new VerifyPaymentIntentCommand(paymentIntentId));
    }

    public Task<PaymentSessionResponse> ReconcileAsync(string paymentIntentId) => SendAsync(new VerifyPaymentIntentCommand(paymentIntentId));

    public Task<PaymentSessionResponse> CancelAsync(string sessionId, string? idempotencyKey = null)
        => SendAsync(new CancelPaymentSessionCommand(idempotencyKey ?? NewKey(), sessionId));

    public Task<PaymentSessionResponse> GetAsync(string sessionId) => SendAsync(new GetPaymentSessionByIdQuery(sessionId));

    public Task<int> ExpireDueAsync() => SendAsync(new ExpireDuePaymentsCommand(100));

    public IReadOnlyList<PaymentSessionChanged> ChangesOf(string sessionId)
        => Outbox.Written.OfType<PaymentSessionChanged>().Where(change => change.PaymentSessionId == sessionId).ToList();

    public IReadOnlyList<PaymentPaidUnapplied> PaidUnapplied() => Outbox.Written.OfType<PaymentPaidUnapplied>().ToList();

    public IReadOnlyList<MockOperation> OperationsOf(MockOperationKind kind) => Ledger.Operations.Where(operation => operation.Kind == kind).ToList();

    public static PaymentIntentView Leg(PaymentSessionResponse response, TenderType tenderType)
        => response.PaymentIntents.Last(intent => intent.TenderType == tenderType);

    public static async Task<BusinessException> AssertRejectedAsync(int code, Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(action);
        Assert.Equal(code, exception.Code);
        return exception;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    private static PaymentInitiatorContextView DefaultInitiator(PaymentInteractionMode interactionMode) => interactionMode switch
    {
        PaymentInteractionMode.UnattendedApi => AgencyApi,
        PaymentInteractionMode.StaffAssisted => BackOffice(),
        _ => Web
    };

    private void WrapTenderProvidersForCrashes(IServiceCollection services)
    {
        var registrations = services.Where(descriptor => descriptor.ServiceType == typeof(ITenderProvider)).ToList();

        foreach (var registration in registrations)
        {
            services.Remove(registration);
            services.AddScoped<ITenderProvider>(provider =>
            {
                var inner = registration.ImplementationFactory is { } factory
                    ? (ITenderProvider)factory(provider)
                    : (ITenderProvider)ActivatorUtilities.CreateInstance(provider, registration.ImplementationType!);

                return new CrashingTenderProvider(inner, () => _crashBeforeDispatch.Remove(inner.TenderType));
            });
        }
    }
}
