using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;

namespace AeroTech.JetPay.Domain.Providers.Tenders
{
    public sealed record ProviderStartRequest(
        string IdempotencyKey,
        string ProviderProfileId,
        string PaymentSessionId,
        string PaymentIntentId,
        decimal Amount,
        int CurrencyId,
        string? FundingReference,
        string? ReturnUrl,
        DateTimeOffset? SessionExpiresAt);

    public sealed record ProviderOperationRequest(
        string IdempotencyKey,
        string ProviderProfileId,
        string? ProviderTransactionRef,
        decimal Amount,
        int CurrencyId);

    public enum ProviderResultKind
    {
        CustomerActionRequired = 1,
        Succeeded = 2,
        Declined = 3,
        UnavailableBeforeEffect = 4,
        Unknown = 5,
        NotYetPaid = 6,
        NoEffect = 7
    }

    public sealed record ProviderResult(
        ProviderResultKind Kind,
        string? ProviderTransactionRef,
        CustomerAction? CustomerAction,
        DateTimeOffset? PaidAt,
        string? FailureCode,
        string? FailureReason)
    {
        public static ProviderResult CustomerActionRequired(CustomerAction action, string providerTransactionRef)
            => new(ProviderResultKind.CustomerActionRequired, providerTransactionRef, action, null, null, null);

        public static ProviderResult Succeeded(string? providerTransactionRef, DateTimeOffset? paidAt = null)
            => new(ProviderResultKind.Succeeded, providerTransactionRef, null, paidAt, null, null);

        public static ProviderResult Declined(string failureCode, string failureReason, string? providerTransactionRef = null)
            => new(ProviderResultKind.Declined, providerTransactionRef, null, null, failureCode, failureReason);

        public static ProviderResult UnavailableBeforeEffect()
            => new(ProviderResultKind.UnavailableBeforeEffect, null, null, null, "ProviderUnavailable", "The provider route refused before any external effect.");

        public static ProviderResult Unknown(string? providerTransactionRef)
            => new(ProviderResultKind.Unknown, providerTransactionRef, null, null, null, null);

        public static ProviderResult NotYetPaid()
            => new(ProviderResultKind.NotYetPaid, null, null, null, null, null);

        public static ProviderResult NoEffect()
            => new(ProviderResultKind.NoEffect, null, null, null, "NoProviderEffect", "The provider has no payment for this attempt.");
    }
}
