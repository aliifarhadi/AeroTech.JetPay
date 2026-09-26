using System.Globalization;
using AeroTech.JetPay.Domain.Providers.Profiles;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Mock.Funding
{
    public sealed class MockProviderProfileCatalog : IProviderProfileCatalog
    {
        private readonly MockFundingLedger _ledger;

        public MockProviderProfileCatalog(MockFundingLedger ledger) => _ledger = ledger;

        public Task<IReadOnlyList<ProviderProfile>> ListActiveAsync(TenderType tenderType, int currencyId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>(_ledger.Profiles()
                .Select(ToProfile)
                .Where(profile => profile.TenderType == tenderType
                                  && profile.Status == ProviderProfileStatus.Active
                                  && profile.SupportedCurrencies.Contains(currencyId))
                .ToList());

        public Task<ProviderProfile?> FindAsync(string providerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(_ledger.Profile(providerProfileId) is { } settings ? ToProfile(settings) : null);

        private static ProviderProfile ToProfile(MockProviderProfileSettings settings)
            => new(
                settings.Id,
                settings.TenderType,
                settings.Enabled ? ProviderProfileStatus.Active : ProviderProfileStatus.Suspended,
                settings.Revision.ToString(CultureInfo.InvariantCulture),
                settings.CurrencyIds,
                settings.TenderType == TenderType.IranianPgw ? AmountUnit.Irr : AmountUnit.MajorCurrency,
                settings.MinimumAmount,
                settings.MaximumAmount,
                settings.SupportsInquiry,
                SupportsRefund: false,
                SupportsPartialRefund: false,
                SupportsReversal: settings.TenderType == TenderType.IranianPgw,
                settings.RequiresSettlementAfterVerify,
                settings.SupportsProviderIdempotency,
                SupportsPartialAmount: false,
                settings.UnverifiedPaymentExpiryBehavior,
                settings.VerifyWindowSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null);
    }
}
