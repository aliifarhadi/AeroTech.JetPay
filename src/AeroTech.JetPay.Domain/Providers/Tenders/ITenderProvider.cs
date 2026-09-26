using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.Providers.Tenders
{
    public interface ITenderProvider
    {
        TenderType TenderType { get; }

        Task<ProviderResult> StartAsync(ProviderStartRequest request, CancellationToken cancellationToken = default);

        Task<ProviderResult> VerifyAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default);

        Task<ProviderResult> SettleAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default);

        Task<ProviderResult> InquireAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default);
    }
}
