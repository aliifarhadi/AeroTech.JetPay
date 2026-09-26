using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.Providers.Tenders
{
    public sealed record PaymentMethodOption(
        string Id,
        TenderType TenderType,
        string DisplayCode,
        int CurrencyId,
        decimal? AvailableAmount,
        decimal? MinimumAmount,
        decimal? MaximumAmount,
        bool SupportsPartialAmount,
        bool IsDefault,
        bool CanAutoSelect,
        CustomerActionType CustomerActionType,
        IReadOnlyList<PaymentAssuranceRequirement> SupportedAssuranceRequirements,
        DateTimeOffset? ExpiresAt,
        string? FundingReference);
}
