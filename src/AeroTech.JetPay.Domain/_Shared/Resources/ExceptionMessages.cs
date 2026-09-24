namespace AeroTech.JetPay.Domain._Shared.Resources
{
    public static class ExceptionMessages
    {
        // Payment intent lifecycle
        public const string PaymentIntentNotFound = "Payment intent '{0}' was not found.";
        public const string PaymentIntentCannotTransition = "Payment intent '{0}' in status {1} cannot {2}.";
        public const string PaymentIntentOperationInProgress = "Another operation is already in progress for '{0}'. Read the payment intent back or retry shortly.";
        public const string PaymentIntentExpired = "Payment intent '{0}' has expired.";
        public const string PaymentIntentOutcomePending = "Payment intent '{0}' has a provider outcome pending and cannot be cancelled until it is resolved.";
        public const string CapturedPaymentIntentCannotBeCancelled = "Payment intent '{0}' has captured {1}; captured money is returned through a refund, not a cancellation.";
        public const string CaptureIsNotAllowedForCaptureMode = "Payment intent '{0}' uses capture mode {1}; it cannot be captured explicitly.";
        public const string CaptureAmountExceedsCapturable = "Capture amount {1} of payment intent '{0}' must be greater than zero and at most the capturable amount {2}.";
        public const string PaymentAmountMustBePositive = "Payment amount {0} must be greater than zero.";
        public const string IntentExpiryMustBeInTheFuture = "Intent expiry {0} must be later than {1}.";

        // Idempotency and instruction identity
        public const string IdempotencyKeyReusedWithDifferentPayload = "Idempotency key '{0}' was already used for {1} with a different payload.";
        public const string PayableInstructionConflict = "Payable instruction '{0}' is already bound to active payment intent '{1}' with different terms.";

        // Payment method selection
        public const string PaymentMethodOptionNotAvailable = "Payment method option '{0}' is not available for payment intent '{1}'.";
        public const string PaymentMethodOptionIncompatible = "Payment method option '{0}' does not support guarantee {1} with capture mode {2}.";

        // Tender providers
        public const string TenderProviderNotRegistered = "No tender provider is registered for tender type {0}.";
        public const string ProviderOutcomeCannotBeRecorded = "Payment intent '{0}' in status {1} cannot record a {2} provider outcome.";
        public const string ProviderOutcomeIsNotExpected = "The provider returned {1} for payment intent '{0}', which the intent's terms do not allow.";
        public const string ProviderAmountMismatch = "The provider reported {1} for payment intent '{0}', but {2} was requested.";
        public const string ProviderDeclinedCapture = "The provider declined the capture of payment intent '{0}' with code {1}.";
        public const string ProviderDeclinedRelease = "The provider declined the release of payment intent '{0}' with code {1}.";

        // Invariants
        public const string PaidUnappliedRequiresCapturedMoney = "Payment intent '{0}' has no captured money to report as paid-unapplied.";
        public const string InvariantViolation = "Payment intent '{0}' violates its amount invariants: {1}.";
    }
}
