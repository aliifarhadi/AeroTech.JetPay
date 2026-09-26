using AeroTech.JetPay.Domain.Providers.Funding;
using AeroTech.Messages.JetPay.Enums;

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
    }
}
