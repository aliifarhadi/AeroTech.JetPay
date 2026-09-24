using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.Messages.JetPay.IntegrationEvents.V1
{
    public sealed record PaymentIntentChangedV1(
        string PaymentIntentId,
        string PayableInstructionId,
        long OrderId,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PaymentIntentStatus Status,
        RequiredGuarantee RequiredGuarantee,
        PaymentCaptureMode CaptureMode,
        decimal RequestedAmount,
        decimal AuthorizedAmount,
        decimal GuaranteedAmount,
        decimal CapturedAmount,
        int CurrencyId,
        DateTimeOffset? GuaranteeExpiresAt,
        DateTimeOffset? IntentExpiresAt,
        long Version,
        string? FailureCode,
        DateTimeOffset OccurredAt) : BaseIntegrationEvent;
}
