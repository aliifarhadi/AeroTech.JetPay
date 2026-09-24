using AeroTech.JetPay.Domain.PaymentIntentAggregate.ValueObjects;

namespace AeroTech.JetPay.Domain.Providers.Tenders
{
    public enum TenderOutcomeKind
    {
        RequiresCustomerAction = 1,
        Processing = 2,
        Authorized = 3,
        Captured = 4,
        Failed = 5,
        Released = 6
    }

    public sealed record TenderOutcome(
        TenderOutcomeKind Kind,
        decimal Amount,
        bool IssuanceGuarantee,
        DateTimeOffset? AuthorizationExpiresAt,
        CustomerAction? CustomerAction,
        string? FailureCode,
        string? FailureReason)
    {
        public static TenderOutcome RequiresCustomerAction(CustomerAction action)
            => new(TenderOutcomeKind.RequiresCustomerAction, 0, false, null, action, null, null);

        public static TenderOutcome Processing()
            => new(TenderOutcomeKind.Processing, 0, false, null, null, null, null);

        public static TenderOutcome Authorized(decimal amount, bool issuanceGuarantee, DateTimeOffset? authorizationExpiresAt)
            => new(TenderOutcomeKind.Authorized, amount, issuanceGuarantee, authorizationExpiresAt, null, null, null);

        public static TenderOutcome Captured(decimal amount)
            => new(TenderOutcomeKind.Captured, amount, false, null, null, null, null);

        public static TenderOutcome Failed(string failureCode, string failureReason)
            => new(TenderOutcomeKind.Failed, 0, false, null, null, failureCode, failureReason);

        public static TenderOutcome Released()
            => new(TenderOutcomeKind.Released, 0, false, null, null, null, null);
    }
}
