namespace AeroTech.JetPay.Domain.IdempotencyRecordAggregate
{
    public enum IdempotentOperation
    {
        CreatePaymentIntent = 1,
        ConfirmPaymentIntent = 2,
        CapturePaymentIntent = 3,
        CancelPaymentIntent = 4
    }
}
