namespace AeroTech.JetPay.Jobs.PaymentSessionAggregate
{
    public sealed class PaymentExpiryOptions
    {
        public const string SectionName = "PaymentExpiry";

        public int IntervalSeconds { get; set; } = 60;

        public int MaxBatch { get; set; } = 100;
    }
}
