using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.PaymentIntentAggregate.Arguments
{
    public sealed record CreatePaymentIntentArgs(
        string PayableInstructionId,
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PayerType PayerType,
        long PayerId,
        decimal Amount,
        int CurrencyId,
        RequiredGuarantee RequiredGuarantee,
        PaymentCaptureMode CaptureMode,
        DateTimeOffset? IntentExpiresAt);
}
