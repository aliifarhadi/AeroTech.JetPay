using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.AcceptanceTests.Fakes;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CancelPaymentIntent;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CapturePaymentIntent;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.ConfirmPaymentIntent;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CreatePaymentIntent;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.ExpireDuePaymentIntents;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.VerifyPaymentIntent;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Queries.GetPaymentIntentById;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Mock;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.JetPay.Mock.Tenders;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.JetPay.IntegrationEvents.V1;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fixtures;

/// <summary>
/// Drives MockJetPay through the real MediatR pipeline (validation included) with the real mock tender adapters;
/// only persistence, clock, ids, lock and outbox are in-memory fakes.
/// </summary>
public sealed class PaymentHarness : IDisposable
{
    public const long PayerId = 7001;
    public const int CurrencyId = 364;
    public const decimal Amount = 1_250_000m;

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private int _keys;

    public PaymentHarness(Action<MockJetPayOptions>? configureMock = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentIntents:LockExpirySeconds"] = "30",
                ["MockJetPay:Enabled"] = "true",
                ["MockJetPay:PublicBaseUrl"] = "http://mock.jetpay.test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(configuration);
        services.AddMockJetPay(configuration);

        if (configureMock is not null)
            services.PostConfigure(configureMock);

        services.Replace(ServiceDescriptor.Singleton<IClock>(Clock));
        services.AddSingleton<IIdGenerator>(Ids);
        services.AddSingleton<IDistributedLock>(Lock);
        services.AddSingleton<IOutboxWriter>(Outbox);
        services.AddScoped<InMemoryUnitOfWork>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<InMemoryUnitOfWork>());
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

    public MockScenarioRegistry Scenarios => _provider.GetRequiredService<MockScenarioRegistry>();

    public InMemoryPaymentIntentRepository Intents => _scope.ServiceProvider.GetRequiredService<InMemoryPaymentIntentRepository>();

    public InMemoryIdempotencyRecordRepository IdempotencyRecords => _scope.ServiceProvider.GetRequiredService<InMemoryIdempotencyRecordRepository>();

    public string NewKey() => $"idem-{Interlocked.Increment(ref _keys)}";

    public Task<T> SendAsync<T>(IRequest<T> request) => _scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);

    public static CreatePaymentIntentCommand CreateCommand(
        string idempotencyKey,
        long orderId = 5001,
        int commercialVersion = 1,
        decimal amount = Amount,
        PayerType payerType = PayerType.Customer,
        RequiredGuarantee guarantee = RequiredGuarantee.PaidBeforeIssuance,
        PaymentCaptureMode captureMode = PaymentCaptureMode.Automatic,
        DateTimeOffset? intentExpiresAt = null,
        string? payableInstructionId = null)
        => new(
            idempotencyKey,
            payableInstructionId ?? $"payable-{orderId}-v{commercialVersion}",
            orderId,
            $"ORD-{orderId}",
            commercialVersion,
            PaymentPurpose.InitialSale,
            payerType,
            PayerId,
            amount,
            CurrencyId,
            guarantee,
            captureMode,
            intentExpiresAt);

    public Task<PaymentIntentView> CreateAsync(
        long orderId = 5001,
        int commercialVersion = 1,
        PayerType payerType = PayerType.Customer,
        RequiredGuarantee guarantee = RequiredGuarantee.PaidBeforeIssuance,
        PaymentCaptureMode captureMode = PaymentCaptureMode.Automatic,
        DateTimeOffset? intentExpiresAt = null)
        => SendAsync(CreateCommand(NewKey(), orderId, commercialVersion, Amount, payerType, guarantee, captureMode, intentExpiresAt));

    /// <summary>Creates an intent whose terms fit the Stage-3 option for <paramref name="tender"/>.</summary>
    public Task<PaymentIntentView> CreateForAsync(TenderType tender, long orderId = 5001, int commercialVersion = 1, DateTimeOffset? intentExpiresAt = null)
        => tender switch
        {
            TenderType.IranianPgw or TenderType.StoredValue
                => CreateAsync(orderId, commercialVersion, PayerType.Customer, RequiredGuarantee.PaidBeforeIssuance, PaymentCaptureMode.Automatic, intentExpiresAt),
            TenderType.Bnpl
                => CreateAsync(orderId, commercialVersion, PayerType.Customer, RequiredGuarantee.AuthorizedBeforeIssuance, PaymentCaptureMode.Manual, intentExpiresAt),
            TenderType.AgencyCredit
                => CreateAsync(orderId, commercialVersion, PayerType.Agency, RequiredGuarantee.AuthorizedBeforeIssuance, PaymentCaptureMode.Manual, intentExpiresAt),
            _ => throw new ArgumentOutOfRangeException(nameof(tender), tender, "Not a Stage-3 tender.")
        };

    public static string OptionFor(TenderType tender) => tender switch
    {
        TenderType.IranianPgw => MockPaymentMethodOptionCatalog.IranianPgwOptionId,
        TenderType.StoredValue => MockPaymentMethodOptionCatalog.StoredValueOptionId,
        TenderType.Bnpl => MockPaymentMethodOptionCatalog.BnplOptionId,
        TenderType.AgencyCredit => MockPaymentMethodOptionCatalog.AgencyCreditOptionId,
        _ => throw new ArgumentOutOfRangeException(nameof(tender), tender, "Not a Stage-3 tender.")
    };

    public async Task<PaymentIntentView> CreateAndConfirmAsync(TenderType tender, long orderId = 5001, int commercialVersion = 1)
    {
        var intent = await CreateForAsync(tender, orderId, commercialVersion);
        return await ConfirmAsync(intent.Id, OptionFor(tender));
    }

    public Task<PaymentIntentView> ConfirmAsync(string paymentIntentId, string optionId, string? idempotencyKey = null, string? returnUrl = null)
        => SendAsync(new ConfirmPaymentIntentCommand(idempotencyKey ?? NewKey(), paymentIntentId, optionId, returnUrl));

    public Task<PaymentIntentView> CompleteCustomerActionAsync(string paymentIntentId)
        => SendAsync(new VerifyPaymentIntentCommand(paymentIntentId));

    public Task<PaymentIntentView> CaptureAsync(string paymentIntentId, decimal? amount = null, bool finalCapture = true, string? idempotencyKey = null)
        => SendAsync(new CapturePaymentIntentCommand(idempotencyKey ?? NewKey(), paymentIntentId, amount, finalCapture));

    public Task<PaymentIntentView> CancelAsync(
        string paymentIntentId,
        PaymentCancellationReason reason = PaymentCancellationReason.Abandoned,
        string? idempotencyKey = null)
        => SendAsync(new CancelPaymentIntentCommand(idempotencyKey ?? NewKey(), paymentIntentId, reason));

    public Task<PaymentIntentView> GetAsync(string paymentIntentId) => SendAsync(new GetPaymentIntentByIdQuery(paymentIntentId));

    public Task<int> ExpireDueAsync() => SendAsync(new ExpireDuePaymentIntentsCommand(100));

    public Task<IReadOnlyList<PaymentMethodOptionView>> ResolveAsync(PayerType payerType, long orderId = 5001)
        => SendAsync(new ResolvePaymentMethodOptionsQuery(orderId, $"ORD-{orderId}", 1, payerType, PayerId, Amount, CurrencyId, "Web"));

    public IReadOnlyList<PaymentIntentChangedV1> ChangesOf(string paymentIntentId)
        => Outbox.Written.OfType<PaymentIntentChangedV1>().Where(change => change.PaymentIntentId == paymentIntentId).ToList();

    public IReadOnlyList<PaymentPaidUnappliedV1> PaidUnapplied()
        => Outbox.Written.OfType<PaymentPaidUnappliedV1>().ToList();

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
}
