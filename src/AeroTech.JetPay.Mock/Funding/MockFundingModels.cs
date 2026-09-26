using AeroTech.JetPay.Domain.Providers.Profiles;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Mock.Funding
{
    public sealed class MockWallet
    {
        public required string Id { get; init; }

        public required string Code { get; init; }

        public required PayerType PayerType { get; init; }

        public required long PayerId { get; init; }

        public required int CurrencyId { get; init; }

        public decimal Balance { get; set; }

        public bool IsDefault { get; set; }
    }

    public enum MockPgwMode
    {
        Normal = 1,
        UnavailableBeforeEffect = 2,
        UnknownAfterEffect = 3
    }

    public sealed class MockProviderProfileSettings
    {
        public required string Id { get; init; }

        public required TenderType TenderType { get; init; }

        public int Revision { get; set; } = 1;

        public bool Enabled { get; set; } = true;

        public MockPgwMode Mode { get; set; } = MockPgwMode.Normal;

        public int[] CurrencyIds { get; set; } = [];

        public decimal? MinimumAmount { get; set; }

        public decimal? MaximumAmount { get; set; }

        public bool SupportsInquiry { get; set; }

        public bool RequiresSettlementAfterVerify { get; set; }

        public bool SupportsProviderIdempotency { get; set; }

        public UnverifiedPaymentExpiryBehavior UnverifiedPaymentExpiryBehavior { get; set; } = UnverifiedPaymentExpiryBehavior.AutoReverse;

        public int? VerifyWindowSeconds { get; set; }
    }

    public enum MockCustomerOutcome
    {
        Paid = 1,
        Declined = 2
    }

    public enum MockOperationKind
    {
        WalletDebit = 1,
        PgwTransaction = 2
    }

    public sealed class MockOperation
    {
        public required string Key { get; init; }

        public required MockOperationKind Kind { get; init; }

        public required string Reference { get; init; }

        public required string ProviderProfileId { get; init; }

        public required string PaymentIntentId { get; init; }

        public required decimal Amount { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public MockCustomerOutcome? CustomerOutcome { get; set; }

        public DateTimeOffset? PaidAt { get; set; }

        public DateTimeOffset? VerifiedAt { get; set; }

        public DateTimeOffset? SettledAt { get; set; }

        public DateTimeOffset? ReturnedAt { get; set; }

        public int StartCalls { get; set; } = 1;
    }

    public sealed record MockRouteAttempt(string PaymentIntentId, string ProviderProfileId, string Result);
}
