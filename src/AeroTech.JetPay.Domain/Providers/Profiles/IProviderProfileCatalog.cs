using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.Providers.Profiles
{
    public interface IProviderProfileCatalog
    {
        Task<IReadOnlyList<ProviderProfile>> ListActiveAsync(TenderType tenderType, int currencyId, CancellationToken cancellationToken = default);

        Task<ProviderProfile?> FindAsync(string providerProfileId, CancellationToken cancellationToken = default);
    }
}
