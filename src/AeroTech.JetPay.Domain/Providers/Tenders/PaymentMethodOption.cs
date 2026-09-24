using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.Providers.Tenders
{
    public sealed record PaymentMethodOption(
        string Id,
        TenderType TenderType,
        string DisplayCode,
        CustomerActionType CustomerActionType,
        IReadOnlyList<RequiredGuarantee> SupportedGuarantees,
        IReadOnlyList<PaymentCaptureMode> SupportedCaptureModes)
    {
        public bool Supports(RequiredGuarantee guarantee, PaymentCaptureMode captureMode)
            => SupportedGuarantees.Contains(guarantee) && SupportedCaptureModes.Contains(captureMode);
    }

    public sealed record PaymentMethodEligibilityQuery(
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PayerType PayerType,
        long PayerId,
        decimal Amount,
        int CurrencyId,
        string? SalesChannel);
}
