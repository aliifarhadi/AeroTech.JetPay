using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.Providers.Tenders
{
    public interface ITenderProvider
    {
        TenderType TenderType { get; }

        Task<TenderOutcome> StartAsync(TenderStartRequest request, CancellationToken cancellationToken = default);

        Task<TenderOutcome> VerifyAsync(TenderVerifyRequest request, CancellationToken cancellationToken = default);

        Task<TenderOutcome> ReleaseAsync(TenderReleaseRequest request, CancellationToken cancellationToken = default);
    }
}
