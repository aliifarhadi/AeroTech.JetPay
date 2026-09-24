using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.Providers.Tenders
{
    public sealed record TenderStartRequest(
        string PaymentIntentId,
        long OrderId,
        PayerType PayerType,
        long PayerId,
        decimal Amount,
        int CurrencyId,
        RequiredGuarantee RequiredGuarantee,
        PaymentCaptureMode CaptureMode,
        DateTimeOffset? IntentExpiresAt,
        string? ReturnUrl,
        string IdempotencyKey);

    public sealed record TenderVerifyRequest(
        string PaymentIntentId,
        long OrderId,
        decimal Amount,
        int CurrencyId,
        RequiredGuarantee RequiredGuarantee,
        string IdempotencyKey);

    public sealed record TenderCaptureRequest(
        string PaymentIntentId,
        decimal Amount,
        int CurrencyId,
        bool FinalCapture,
        string IdempotencyKey);

    public sealed record TenderReleaseRequest(
        string PaymentIntentId,
        PaymentCancellationReason Reason,
        string IdempotencyKey);
}
