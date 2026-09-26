namespace AeroTech.JetPay.Application.PaymentMethodOptions.Services
{
    public sealed class PaymentAcceptancePolicyOptions
    {
        public const string SectionName = "PaymentAcceptancePolicy";

        public bool AllowPgwForStaffAssisted { get; set; }
    }
}
