namespace AeroTech.JetPay.Mock.Configuration
{
    public sealed class MockJetPayOptions
    {
        public const string SectionName = "MockJetPay";

        public bool Enabled { get; set; }

        public string PublicBaseUrl { get; set; } = "http://localhost:4747";

        public int CustomerActionTtlSeconds { get; set; } = 900;

        public int VerifyWindowSeconds { get; set; } = 600;

        public int[] PgwCurrencyIds { get; set; } = [70];

        public int[] StoredValueCurrencyIds { get; set; } = [70];
    }
}
