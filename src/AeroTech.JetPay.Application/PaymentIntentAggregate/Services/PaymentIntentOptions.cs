namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Services
{
    public sealed class PaymentIntentOptions
    {
        public const string SectionName = "PaymentIntents";

        public int LockExpirySeconds { get; set; } = 30;
    }
}
