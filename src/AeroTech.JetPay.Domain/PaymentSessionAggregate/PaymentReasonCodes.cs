namespace AeroTech.JetPay.Domain.PaymentSessionAggregate
{
    public static class FundingFailureCode
    {
        public const string DefaultFundingSourceUnavailable = "DefaultFundingSourceUnavailable";
        public const string InsufficientFunds = "InsufficientFunds";
    }

    public static class PaidUnappliedReason
    {
        public const string PayableInstructionSuperseded = "PayableInstructionSuperseded";
        public const string PaymentSessionCancelled = "PaymentSessionCancelled";
        public const string PaymentSessionExpired = "PaymentSessionExpired";
        public const string PaymentIntentClosed = "PaymentIntentClosed";
    }
}
