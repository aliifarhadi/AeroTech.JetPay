using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fakes;

public sealed class CrashingTenderProvider(ITenderProvider inner, Func<bool> shouldCrash) : ITenderProvider
{
    public TenderType TenderType => inner.TenderType;

    public Task<ProviderResult> StartAsync(ProviderStartRequest request, CancellationToken cancellationToken = default)
        => shouldCrash()
            ? throw new InvalidOperationException($"Simulated crash before dispatching {TenderType}.")
            : inner.StartAsync(request, cancellationToken);

    public Task<ProviderResult> VerifyAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default)
        => inner.VerifyAsync(request, cancellationToken);

    public Task<ProviderResult> SettleAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default)
        => inner.SettleAsync(request, cancellationToken);

    public Task<ProviderResult> InquireAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default)
        => inner.InquireAsync(request, cancellationToken);
}
