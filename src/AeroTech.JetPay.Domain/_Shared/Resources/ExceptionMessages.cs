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
        public const string ProviderEffectUnresolved = "Payment session '{0}' has a provider payment whose outcome is still unresolved; no other payment route can start until it is resolved.";
        public const string PaymentAmountMustNotBeNegative = "Payment amount {0} must not be negative.";
        public const string SessionExpiryMustBeInTheFuture = "Session expiry {0} must be later than {1}.";
        public const string CurrencyMismatch = "Currency {1} does not match the payment session currency {0}.";
        public const string ProviderAttemptCannotTransition = "Provider attempt {0} in status {1} cannot {2}.";

        public const string IdempotencyKeyReusedWithDifferentPayload = "Idempotency key '{0}' was already used for {1} with a different payload.";
        public const string PayableInstructionConflict = "Payable instruction '{0}' is already bound to active payment session '{1}' with different terms.";

        public const string PaymentMethodOptionNotAvailable = "Payment method option '{0}' is not eligible for payment session '{1}'.";
        public const string SelectionsRequired = "At least one payment selection is required.";
        public const string SelectionsOnlyForExplicit = "Selections can be supplied at creation only when SelectionMode is Explicit.";
        public const string DuplicateSelection = "Payment method option '{0}' is selected more than once.";
        public const string SelectionSumMismatch = "Selections total {0}, but the amount still to fund is {1}.";
        public const string SelectionExceedsAvailableAmount = "Selection of {1} on option '{0}' exceeds its available amount {2}.";
        public const string SelectionOutsideAmountLimits = "Selection of {1} on option '{0}' is outside its limits [{2}, {3}].";
        public const string PartialAmountNotSupported = "Option '{0}' cannot fund part of the payment; it must cover the whole outstanding amount.";
        public const string MultipleSelectionsNotSupported = "Only one payment selection per request is supported at this stage.";

        public const string TenderProviderNotRegistered = "No tender provider is registered for tender type {0}.";
        public const string ProviderProfileNotFound = "Provider profile '{0}' was not found.";
        public const string NoProviderAttempt = "Payment intent '{0}' has no provider attempt to act on.";

        public const string PaidUnappliedRequiresCapturedMoney = "Payment intent '{0}' has no captured money to report as paid-unapplied.";
        public const string InvariantViolation = "'{0}' violates its invariants: {1}.";
    }
}
