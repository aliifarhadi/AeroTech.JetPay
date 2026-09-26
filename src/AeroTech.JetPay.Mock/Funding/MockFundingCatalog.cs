using AeroTech.JetPay.Domain.Providers.Funding;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.JetPay.Mock.Funding
{
    public sealed class MockFundingCatalog : IFundingCatalog
    {
        private readonly MockFundingLedger _ledger;

        public MockFundingCatalog(MockFundingLedger ledger) => _ledger = ledger;

        public Task<IReadOnlyList<FundingAccount>> ListStoredValueAccountsAsync(PayerType payerType, long payerId, int currencyId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FundingAccount>>(_ledger.WalletsOf(payerType, payerId, currencyId)
                .Select(wallet => new FundingAccount(wallet.Id, TenderType.StoredValue, wallet.CurrencyId, wallet.Balance, wallet.IsDefault))
                .ToList());

        public Task<IReadOnlyList<FundingAccount>> ListCreditFacilitiesAsync(PayerType payerType, long payerId, int currencyId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FundingAccount>>(_ledger.FacilitiesOf(payerType, payerId, currencyId)
                .Select(facility => new FundingAccount(facility.Id, facility.TenderType, facility.CurrencyId, facility.Available, IsDefault: false))
                .ToList());

        public Task<bool> IsCashAcceptedAsync(long? officeId, SalesChannel salesChannel, CancellationToken cancellationToken = default)
            => Task.FromResult(_ledger.IsCashAccepted(officeId, salesChannel));

        public Task<TenderAvailability?> FindBnplAvailabilityAsync(PayerType payerType, long payerId, int currencyId, CancellationToken cancellationToken = default)
            => Task.FromResult(_ledger.IsBnplAvailable(payerType, payerId, currencyId)
                ? new TenderAvailability(_ledger.BnplMinimumAmount, _ledger.BnplMaximumAmount)
                : null);

        public Task<TenderAvailability?> FindPgwAvailabilityAsync(int currencyId, CancellationToken cancellationToken = default)
        {
            var routes = _ledger.PgwRoutes(currencyId);

            if (routes.Count == 0)
                return Task.FromResult<TenderAvailability?>(null);

            var minimum = routes.All(route => route.MinimumAmount is not null) ? routes.Min(route => route.MinimumAmount) : null;
            var maximum = routes.All(route => route.MaximumAmount is not null) ? routes.Max(route => route.MaximumAmount) : null;

            return Task.FromResult<TenderAvailability?>(new TenderAvailability(minimum, maximum));
        }
    }
}
