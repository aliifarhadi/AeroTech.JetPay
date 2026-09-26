using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fakes;

public sealed class CrashingTenderProvider(ITenderProvider inner, Func<bool> shouldCrash) : ITenderProvider
{
    public TenderType TenderType => inner.TenderType;

    public Task<TenderOutcome> StartAsync(TenderStartRequest request, CancellationToken cancellationToken = default)
        => shouldCrash()
            ? throw new InvalidOperationException($"Simulated crash before dispatching {TenderType}.")
            : inner.StartAsync(request, cancellationToken);

    public Task<TenderOutcome> VerifyAsync(TenderVerifyRequest request, CancellationToken cancellationToken = default)
        => inner.VerifyAsync(request, cancellationToken);

    public Task<TenderOutcome> ReleaseAsync(TenderReleaseRequest request, CancellationToken cancellationToken = default)
        => inner.ReleaseAsync(request, cancellationToken);
}
