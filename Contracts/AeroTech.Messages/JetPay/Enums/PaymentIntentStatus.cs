namespace AeroTech.Messages.JetPay.Enums
{
    public enum PaymentIntentStatus
    {
        Created = 1,
        RequiresCustomerAction = 2,
        Processing = 3,
        Authorized = 4,
        PartiallyCaptured = 5,
        Captured = 6,
        Failed = 7,
        Cancelled = 8,
        Expired = 9
    }
}
