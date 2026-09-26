using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.Providers.Profiles
{
    public enum ProviderProfileStatus
    {
        Active = 1,
        Suspended = 2,
        Retired = 3
    }

    public enum AmountUnit
    {
        MajorCurrency = 1,
        Irr = 2,
        Toman = 3,
        ProviderDefined = 4
    }

    public enum UnverifiedPaymentExpiryBehavior
    {
        AutoReverse = 1,
        InquiryRequired = 2,
        ManualReconciliation = 3
    }

    public sealed record ProviderProfile(
        string Id,
        TenderType TenderType,
        ProviderProfileStatus Status,
        string Version,
        IReadOnlyList<int> SupportedCurrencies,
        AmountUnit AmountUnit,
        decimal? MinimumAmount,
        decimal? MaximumAmount,
        bool SupportsInquiry,
        bool SupportsRefund,
        bool SupportsPartialRefund,
        bool SupportsReversal,
        bool RequiresSettlementAfterVerify,
        bool SupportsProviderIdempotency,
        bool SupportsPartialAmount,
        UnverifiedPaymentExpiryBehavior UnverifiedPaymentExpiryBehavior,
        TimeSpan? DefaultVerifyWindow)
    {
        public bool Accepts(int currencyId, decimal amount)
            => Status == ProviderProfileStatus.Active
               && SupportedCurrencies.Contains(currencyId)
               && !(amount < MinimumAmount)
               && !(amount > MaximumAmount);

        public DateTimeOffset? AutoReversalBoundary(DateTimeOffset from)
            => UnverifiedPaymentExpiryBehavior == UnverifiedPaymentExpiryBehavior.AutoReverse && DefaultVerifyWindow is { } window
                ? from + window
                : null;

        public DateTimeOffset? AutoReversalAt(DateTimeOffset verifyDeadline)
            => UnverifiedPaymentExpiryBehavior == UnverifiedPaymentExpiryBehavior.AutoReverse ? verifyDeadline : null;
    }
}
