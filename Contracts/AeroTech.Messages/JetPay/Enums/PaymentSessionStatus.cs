namespace AeroTech.Messages.JetPay.Enums
{
    public enum PaymentSessionStatus
    {
        Created = 1,
        RequiresPaymentMethod = 2,
        RequiresCustomerAction = 3,
        Processing = 4,
        PartiallyCovered = 5,
        Guaranteed = 6,
        Paid = 7,
        Cancelled = 8,
        Expired = 9
    }
}
