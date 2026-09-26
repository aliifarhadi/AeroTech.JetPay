namespace AeroTech.JetPay.Domain.IdempotencyRecordAggregate
{
    public enum IdempotentOperation
    {
        CreatePaymentSession = 1,
        ConfirmPaymentSession = 2,
        CancelPaymentSession = 3
    }
}
