using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.AcceptanceTests.Fakes;
using AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.AddPaymentSelections;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CancelPaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ExpireDuePayments;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ProcessProviderCallback;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ReconcilePaymentIntent;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Queries.GetPaymentSessionById;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities;
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
    public const long IssuerId = 11;
    public const long EmployeeId = 42;
    public const long OfficeId = 501;
    public const decimal Amount = 1_000_000m;
    public static readonly TimeSpan VerifyWindow = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan CustomerActionTtl = TimeSpan.FromMinutes(15);

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly HashSet<TenderType> _crashBeforeDispatch = [];
    private int _keys;

    public PaymentHarness()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentSessions:LockExpirySeconds"] = "30",
                ["MockJetPay:Enabled"] = "true",
                ["MockJetPay:PublicBaseUrl"] = "http://mock.jetpay.test",
                ["MockJetPay:CustomerActionTtlSeconds"] = CustomerActionTtl.TotalSeconds.ToString(),
                ["MockJetPay:VerifyWindowSeconds"] = VerifyWindow.TotalSeconds.ToString()
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

    public static PaymentInitiatorContextView Web => new("Customer", CustomerId, SalesChannel.IBE, null);

    public static PaymentInitiatorContextView AgencyApi => new("AgencyApiPrincipal", 88, SalesChannel.PartnerAPI, null);

    public static PaymentInitiatorContextView BackOffice => new("AirlineEmployee", EmployeeId, SalesChannel.BackOffice, OfficeId);

    public MockWallet Wallet(PayerType payerType, long payerId, decimal balance, bool isDefault = false, int currencyId = Irr, string code = "main")
        => Ledger.SetWallet(payerType, payerId, currencyId, code, balance, isDefault);

    public void ConfigureProfile(string providerProfileId, Action<MockProviderProfileSettings> configure)
        => Ledger.ConfigureProfile(providerProfileId, configure);

    public void CrashBeforeNextDispatchOf(TenderType tenderType) => _crashBeforeDispatch.Add(tenderType);

    public string NewKey() => $"idem-{Interlocked.Increment(ref _keys)}";

    public Task<T> SendAsync<T>(IRequest<T> request) => _scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);

    public static CreatePaymentSessionCommand CreateCommand(
        string idempotencyKey,
        PayerType payerType = PayerType.Customer,
        long payerId = CustomerId,
        PaymentInteractionMode interactionMode = PaymentInteractionMode.CustomerInteractive,
        PaymentSelectionMode? selectionMode = null,
        decimal amount = Amount,
        int currencyId = Irr,
        long orderId = 5001,
        int commercialVersion = 1,
        DateTimeOffset? expiresAt = null,
        IReadOnlyList<PaymentSelection>? selections = null,
        long issuerLegalEntityId = IssuerId,
        PaymentAssuranceRequirement assurance = PaymentAssuranceRequirement.IssuanceGuaranteed)
        => new(
            idempotencyKey,
            $"payable-{orderId}-v{commercialVersion}",
            orderId,
            $"ORD-{orderId}",
            commercialVersion,
            PaymentPurpose.InitialSale,
            issuerLegalEntityId,
            payerType,
            payerId,
            DefaultInitiator(interactionMode),
            interactionMode,
            selectionMode ?? DefaultSelection(interactionMode),
            amount,
            currencyId,
            assurance,
            expiresAt,
            selections ?? []);

    public Task<PaymentSessionView> CreateAsync(
        PayerType payerType = PayerType.Customer,
        long payerId = CustomerId,
        PaymentInteractionMode interactionMode = PaymentInteractionMode.CustomerInteractive,
        PaymentSelectionMode? selectionMode = null,
        decimal amount = Amount,
        int currencyId = Irr,
        long orderId = 5001,
        int commercialVersion = 1,
        DateTimeOffset? expiresAt = null,
        IReadOnlyList<PaymentSelection>? selections = null)
        => SendAsync(CreateCommand(NewKey(), payerType, payerId, interactionMode, selectionMode, amount, currencyId, orderId, commercialVersion, expiresAt, selections));

    public Task<PaymentSessionView> CreateAgencyDefaultAsync(decimal amount = Amount, int currencyId = Irr)
        => CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.UnattendedApi, PaymentSelectionMode.Default, amount, currencyId);

    public Task<IReadOnlyList<PaymentMethodOptionView>> ResolveAsync(
        PayerType payerType = PayerType.Customer,
        long payerId = CustomerId,
        PaymentInteractionMode interactionMode = PaymentInteractionMode.CustomerInteractive,
        decimal amount = Amount,
        int currencyId = Irr)
        => SendAsync(new ResolvePaymentMethodOptionsQuery(
            5001,
            "ORD-5001",
            1,
            PaymentPurpose.InitialSale,
            IssuerId,
            payerType,
            payerId,
            DefaultInitiator(interactionMode),
            interactionMode,
            amount,
            currencyId,
            PaymentAssuranceRequirement.IssuanceGuaranteed));

    public Task<IReadOnlyList<PaymentMethodOptionView>> OptionsFor(PaymentSessionView session)
        => SendAsync(new ResolvePaymentMethodOptionsQuery(
            session.OrderId,
            session.OrderReference,
            session.CommercialVersion,
            session.Purpose,
            session.IssuerLegalEntityId,
            session.PayerType,
            session.PayerId,
            session.Initiator,
            session.InteractionMode,
            session.OutstandingAmount,
            session.CurrencyId,
            session.AssuranceRequirement));

    public static string OptionOf(IReadOnlyList<PaymentMethodOptionView> options, TenderType tenderType)
        => options.Single(option => option.TenderType == tenderType).Id;

    public Task<PaymentSessionView> SelectAsync(string sessionId, IReadOnlyList<PaymentSelection> selections, string? idempotencyKey = null, string? returnUrl = null)
        => SendAsync(new AddPaymentSelectionsCommand(idempotencyKey ?? NewKey(), sessionId, selections, returnUrl));

    public async Task<PaymentSessionView> SelectAsync(PaymentSessionView session, TenderType tenderType, string? idempotencyKey = null)
    {
        var options = await OptionsFor(session);
        return await SelectAsync(session.Id, [new PaymentSelection(OptionOf(options, tenderType), session.OutstandingAmount)], idempotencyKey);
    }

    public async Task<(PaymentSessionView Session, PaymentIntentView Intent)> StartPgwAsync(DateTimeOffset? expiresAt = null)
    {
        var session = await CreateAsync(expiresAt: expiresAt);
        var selected = await SelectAsync(session, TenderType.IranianPgw);
        return (selected, selected.Intents.Single());
    }

    public void PayAtGateway(string paymentIntentId, MockCustomerOutcome outcome = MockCustomerOutcome.Paid)
        => Ledger.PayLatest(paymentIntentId, outcome, Clock.Now);

    public Task<PaymentSessionView> CallbackAsync(string paymentIntentId)
        => SendAsync(new ProcessProviderCallbackCommand(paymentIntentId, Ledger.OperationsOf(paymentIntentId).LastOrDefault()?.PaidAt));

    public Task<PaymentSessionView> CompleteAsync(string paymentIntentId, MockCustomerOutcome outcome = MockCustomerOutcome.Paid)
    {
        PayAtGateway(paymentIntentId, outcome);
        return CallbackAsync(paymentIntentId);
    }

    public Task<PaymentSessionView> ReconcileAsync(string paymentIntentId) => SendAsync(new ReconcilePaymentIntentCommand(paymentIntentId));

    public Task<PaymentSessionView> CancelAsync(string sessionId, string? idempotencyKey = null)
        => SendAsync(new CancelPaymentSessionCommand(idempotencyKey ?? NewKey(), sessionId));

    public Task<PaymentSessionView> GetAsync(string sessionId) => SendAsync(new GetPaymentSessionByIdQuery(sessionId));

    public Task<int> SweepAsync() => SendAsync(new ExpireDuePaymentsCommand(100));

    public IReadOnlyList<ProviderPaymentAttempt> AttemptsOf(string paymentIntentId)
        => Sessions.Committed
            .SelectMany(session => session.Intents)
            .Single(intent => intent.Id == paymentIntentId)
            .ProviderAttempts
            .OrderBy(attempt => attempt.AttemptNumber)
            .ToList();

    public IReadOnlyList<PaymentSessionChanged> ChangesOf(string sessionId)
        => Outbox.Written.OfType<PaymentSessionChanged>().Where(change => change.PaymentSessionId == sessionId).ToList();

    public IReadOnlyList<PaymentPaidUnapplied> PaidUnapplied() => Outbox.Written.OfType<PaymentPaidUnapplied>().ToList();

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
        PaymentInteractionMode.StaffAssisted => BackOffice,
        _ => Web
    };

    private static PaymentSelectionMode DefaultSelection(PaymentInteractionMode interactionMode) => interactionMode switch
    {
        PaymentInteractionMode.UnattendedApi => PaymentSelectionMode.Default,
        PaymentInteractionMode.StaffAssisted => PaymentSelectionMode.Explicit,
        _ => PaymentSelectionMode.Interactive
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
