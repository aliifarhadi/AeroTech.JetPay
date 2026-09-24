using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.Providers.Tenders
{
    /// <summary>
    /// JetPay-internal adapter for one tender family. Provider routes, profiles and wire formats stay behind it;
    /// callers only see normalized <see cref="TenderOutcome"/> values.
    /// </summary>
    public interface ITenderProvider
    {
        TenderType TenderType { get; }

        Task<TenderOutcome> StartAsync(TenderStartRequest request, CancellationToken cancellationToken = default);

        /// <summary>Server-side verify/inquiry after a customer action or callback; a callback alone is never payment truth.</summary>
        Task<TenderOutcome> VerifyAsync(TenderVerifyRequest request, CancellationToken cancellationToken = default);

        Task<TenderOutcome> CaptureAsync(TenderCaptureRequest request, CancellationToken cancellationToken = default);

        Task<TenderOutcome> ReleaseAsync(TenderReleaseRequest request, CancellationToken cancellationToken = default);
    }
}
