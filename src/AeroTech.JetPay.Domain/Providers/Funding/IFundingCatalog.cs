using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.Providers.Funding
{
    public sealed record FundingAccount(string Reference, TenderType TenderType, int CurrencyId, decimal AvailableAmount, bool IsDefault);

    public interface IFundingCatalog
    {
        Task<IReadOnlyList<FundingAccount>> ListStoredValueAccountsAsync(PayerType payerType, long payerId, int currencyId, CancellationToken cancellationToken = default);
    }
}
