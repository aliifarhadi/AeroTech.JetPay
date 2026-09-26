namespace AeroTech.JetPay.Domain.PaymentSessionAggregate
{
    public static class FundingFailureCode
    {
        public const string DefaultFundingSourceUnavailable = "DefaultFundingSourceUnavailable";
        public const string InsufficientFunds = "InsufficientFunds";
    }

    public static class IntentFailureCode
    {
        public const string ProviderUnavailable = "ProviderUnavailable";
        public const string VerifyDeadlineElapsed = "VerifyDeadlineElapsed";
        public const string PaymentNoLongerApplicable = "PaymentNoLongerApplicable";
        public const string ProviderReversed = "ProviderReversed";
        public const string NoProviderEffect = "NoProviderEffect";
    }

    public static class PaidUnappliedReason
    {
        public const string PayableInstructionSuperseded = "PayableInstructionSuperseded";
        public const string PaymentSessionCancelled = "PaymentSessionCancelled";
        public const string PaymentSessionExpired = "PaymentSessionExpired";
        public const string PaymentSessionClosed = "PaymentSessionClosed";
    }
}
