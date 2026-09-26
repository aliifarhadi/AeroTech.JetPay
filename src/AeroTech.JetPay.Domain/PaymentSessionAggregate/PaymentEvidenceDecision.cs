namespace AeroTech.JetPay.Domain.PaymentSessionAggregate
{
    public enum PaymentEvidenceDecision
    {
        Verify = 1,
        AlreadyVerified = 2,
        AutoReversal = 3,
        AwaitReconciliation = 4
    }
}
