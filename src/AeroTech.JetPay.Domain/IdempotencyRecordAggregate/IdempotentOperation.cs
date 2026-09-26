namespace AeroTech.JetPay.Domain.IdempotencyRecordAggregate
{
    public enum IdempotentOperation
    {
        CreatePaymentSession = 1,
        AddPaymentSelections = 2,
        CancelPaymentSession = 3
    }
}
