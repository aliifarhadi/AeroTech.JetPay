using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Views
{
    public sealed record PaymentIntentView(
        string Id,
        string PayableInstructionId,
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PayerType PayerType,
        long PayerId,
        decimal RequestedAmount,
        int CurrencyId,
        RequiredGuarantee RequiredGuarantee,
        PaymentCaptureMode CaptureMode,
        PaymentIntentStatus Status,
        decimal AuthorizedAmount,
        decimal GuaranteedAmount,
        decimal CapturedAmount,
        decimal RefundedAmount,
        DateTimeOffset? GuaranteeExpiresAt,
        DateTimeOffset? IntentExpiresAt,
        TenderType? SelectedTenderType,
        string? SelectedPaymentMethodOptionId,
        CustomerActionView? NextAction,
        string? FailureCode,
        string? FailureReason,
        long Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public sealed record CustomerActionView(
        CustomerActionType Type,
        string? Url,
        string? HttpMethod,
        IReadOnlyDictionary<string, string>? FormFields,
        DateTimeOffset? ExpiresAt);
}
