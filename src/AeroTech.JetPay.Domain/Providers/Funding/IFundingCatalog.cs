using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.JetPay.Domain.Providers.Funding
{
    public sealed record FundingAccount(string Reference, TenderType TenderType, int CurrencyId, decimal AvailableAmount, bool IsDefault);

    public sealed record TenderAvailability(decimal? MinimumAmount, decimal? MaximumAmount);

    public interface IFundingCatalog
    {
        Task<IReadOnlyList<FundingAccount>> ListStoredValueAccountsAsync(PayerType payerType, long payerId, int currencyId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FundingAccount>> ListCreditFacilitiesAsync(PayerType payerType, long payerId, int currencyId, CancellationToken cancellationToken = default);

        Task<bool> IsCashAcceptedAsync(long? officeId, SalesChannel salesChannel, CancellationToken cancellationToken = default);

        Task<TenderAvailability?> FindBnplAvailabilityAsync(PayerType payerType, long payerId, int currencyId, CancellationToken cancellationToken = default);

        Task<TenderAvailability?> FindPgwAvailabilityAsync(int currencyId, CancellationToken cancellationToken = default);
    }
}
