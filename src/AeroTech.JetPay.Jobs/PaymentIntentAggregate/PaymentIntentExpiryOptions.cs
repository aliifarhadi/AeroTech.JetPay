namespace AeroTech.JetPay.Jobs.PaymentIntentAggregate
{
    public sealed class PaymentIntentExpiryOptions
    {
        public const string SectionName = "PaymentIntentExpiry";

        public int IntervalSeconds { get; set; } = 60;

        public int MaxBatch { get; set; } = 100;
    }
}
