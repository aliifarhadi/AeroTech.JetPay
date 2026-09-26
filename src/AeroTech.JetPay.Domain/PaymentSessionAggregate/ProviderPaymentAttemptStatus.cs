namespace AeroTech.JetPay.Domain.PaymentSessionAggregate
{
    public enum ProviderPaymentAttemptStatus
    {
        Created = 1,
        CustomerActionPending = 2,
        CallbackReceived = 3,
        VerificationPending = 4,
        Verified = 5,
        Settled = 6,
        AutoReversalPending = 7,
        Reversed = 8,
        Failed = 9,
        Unknown = 10
    }
}
