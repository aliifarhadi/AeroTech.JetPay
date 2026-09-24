namespace AeroTech.JetPay.Domain.PaymentIntentAggregate
{
    public static class PaidUnappliedReason
    {
        public const string PayableInstructionSuperseded = "PayableInstructionSuperseded";
        public const string PaymentIntentCancelled = "PaymentIntentCancelled";
        public const string PaymentIntentExpired = "PaymentIntentExpired";
    }
}
