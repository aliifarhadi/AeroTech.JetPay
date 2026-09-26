namespace AeroTech.JetPay.Domain._Shared.Resources
{
    public static class ExceptionMessages
    {
        public const string PaymentSessionNotFound = "Payment session '{0}' was not found.";
        public const string PaymentSessionCannotTransition = "Payment session '{0}' in status {1} cannot {2}.";
        public const string PaymentOperationInProgress = "Another operation is already in progress for '{0}'. Read the payment session back or retry shortly.";
        public const string PaymentSessionExpired = "Payment session '{0}' has expired.";
        public const string PaymentIntentNotFound = "Payment intent '{0}' was not found.";
        public const string NothingToFund = "Payment session '{0}' has no outstanding amount to fund; pending or successful payments already cover it.";
        public const string PaymentIntentCannotTransition = "Payment intent '{0}' in status {1} cannot {2}.";
        public const string PaymentAmountMustBePositive = "Payment amount {0} must be greater than zero.";
        public const string SessionExpiryMustBeInTheFuture = "Session expiry {0} must be later than {1}.";
        public const string CurrencyMismatch = "Currency {1} does not match the payment session currency {0}.";

        public const string IdempotencyKeyReusedWithDifferentPayload = "Idempotency key '{0}' was already used for {1} with a different payload.";
        public const string PayableInstructionConflict = "Payable instruction '{0}' is already bound to active payment session '{1}' with different terms.";

        public const string PaymentMethodOptionNotAvailable = "Payment method option '{0}' is not eligible for payment session '{1}'.";
        public const string SelectionsRequired = "An explicit funding plan needs at least one selection.";
        public const string SelectionsNotAllowedForDefault = "Default selection lets JetPay choose the funding source; selections must be empty.";
        public const string DuplicateSelection = "Payment method option '{0}' is selected more than once.";
        public const string SelectionSumMismatch = "Selections total {0}, but the amount still to fund is {1}.";
        public const string SelectionExceedsAvailableAmount = "Selection of {1} on option '{0}' exceeds its available amount {2}.";
        public const string SelectionOutsideAmountLimits = "Selection of {1} on option '{0}' is outside its limits [{2}, {3}].";
        public const string PartialAmountNotSupported = "Option '{0}' cannot fund part of the payment; it must cover the whole outstanding amount.";

        public const string TenderProviderNotRegistered = "No tender provider is registered for tender type {0}.";
        public const string ProviderOutcomeCannotBeRecorded = "Payment intent '{0}' in status {1} cannot record a {2} provider outcome.";
        public const string ProviderOutcomeIsNotExpected = "The provider returned {1} for payment intent '{0}', which its terms do not allow.";
        public const string ProviderAmountMismatch = "The provider reported {1} for payment intent '{0}', but {2} was requested.";
        public const string ProviderDeclinedRelease = "The provider declined the release of payment intent '{0}' with code {1}.";

        public const string PaidUnappliedRequiresCapturedMoney = "Payment intent '{0}' has no captured money to report as paid-unapplied.";
        public const string InvariantViolation = "'{0}' violates its amount invariants: {1}.";
    }
}
