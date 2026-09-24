namespace AeroTech.JetPay.Mock.Configuration
{
    public sealed class MockJetPayOptions
    {
        public const string SectionName = "MockJetPay";

        public bool Enabled { get; set; }

        public string PublicBaseUrl { get; set; } = "http://localhost:5061";

        public int CustomerActionTtlSeconds { get; set; } = 900;

        /// <summary>Customer-action lifetime used by the <c>PaymentExpired</c> scenario.</summary>
        public int ExpiringCustomerActionTtlSeconds { get; set; } = 300;

        /// <summary>Authorization validity used by the <c>PaymentExpired</c> scenario for authorization tenders.</summary>
        public int ExpiringAuthorizationValiditySeconds { get; set; } = 300;

        public MockTenderProfileOptions Bnpl { get; set; } = new()
        {
            SupportsIssuanceGuaranteeOnAuthorization = true,
            AuthorizationValiditySeconds = 86_400
        };

        public MockTenderProfileOptions AgencyCredit { get; set; } = new()
        {
            SupportsIssuanceGuaranteeOnAuthorization = true,
            AuthorizationValiditySeconds = null
        };
    }

    public sealed class MockTenderProfileOptions
    {
        public bool SupportsIssuanceGuaranteeOnAuthorization { get; set; }

        /// <summary>Null means the authorization has no known time-limited expiry.</summary>
        public int? AuthorizationValiditySeconds { get; set; }
    }
}
