namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Services
{
    public sealed class PaymentSessionOptions
    {
        public const string SectionName = "PaymentSessions";

        public int LockExpirySeconds { get; set; } = 30;
    }
}
