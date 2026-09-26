namespace AeroTech.Messages.JetPay.Enums
{
    public enum PaymentSessionStatus
    {
        Created = 1,
        RequiresPaymentMethod = 2,
        Processing = 3,
        PartiallyFunded = 4,
        Guaranteed = 5,
        Paid = 6,
        Failed = 7,
        Cancelled = 8,
        Expired = 9
    }
}
