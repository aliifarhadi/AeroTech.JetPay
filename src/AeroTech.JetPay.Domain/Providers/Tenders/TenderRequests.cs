using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.JetPay.Domain.Providers.Tenders
{
    public sealed record TenderStartRequest(
        string PaymentIntentId,
        string PaymentSessionId,
        long OrderId,
        PayerType PayerType,
        long PayerId,
        decimal Amount,
        int CurrencyId,
        string? FundingReference,
        SalesChannel SalesChannel,
        long? OfficeId,
        string? ReturnUrl,
        DateTimeOffset? SessionExpiresAt,
        string IdempotencyKey);

    public sealed record TenderVerifyRequest(
        string PaymentIntentId,
        string PaymentSessionId,
        long OrderId,
        decimal Amount,
        int CurrencyId,
        string? ProviderReference,
        string IdempotencyKey);

    public sealed record TenderReleaseRequest(
        string PaymentIntentId,
        string? ProviderReference,
        string IdempotencyKey);
}
