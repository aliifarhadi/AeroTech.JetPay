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

    public sealed class MockCreditFacility
    {
        public required string Id { get; init; }

        public required TenderType TenderType { get; init; }

        public required PayerType PayerType { get; init; }

        public required long PayerId { get; init; }

        public required int CurrencyId { get; init; }

        public decimal Limit { get; set; }

        public decimal Reserved { get; set; }

        public int? AuthorizationValiditySeconds { get; set; }

        public decimal Available => Limit - Reserved;
    }

    public enum MockPgwMode
    {
        Normal = 1,

        UnavailableBeforeEffect = 2,

        UnknownAfterEffect = 3
    }

    public enum MockInquiryOutcome
    {
        Captured = 1,
        NoEffect = 2
    }

    public sealed class MockPgwProfile
    {
        public required string Code { get; init; }

        public int Priority { get; set; }

        public bool Enabled { get; set; } = true;

        public int[] CurrencyIds { get; set; } = [];

        public MockPgwMode Mode { get; set; } = MockPgwMode.Normal;

        public MockInquiryOutcome InquiryOutcome { get; set; } = MockInquiryOutcome.Captured;

        public decimal? MinimumAmount { get; set; }

        public decimal? MaximumAmount { get; set; }
    }

    public enum MockCustomerOutcome
    {
        Approved = 1,
        Declined = 2
    }

    public enum MockOperationKind
    {
        WalletDebit = 1,
        CreditReservation = 2,
        CashReceipt = 3,
        PgwTransaction = 4,
        BnplApplication = 5
    }

    public sealed class MockOperation
    {
        public required string Key { get; init; }

        public required MockOperationKind Kind { get; init; }

        public required string Reference { get; init; }

        public required decimal Amount { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public string? RouteCode { get; init; }

        public long? OfficeId { get; init; }

        public MockCustomerOutcome? CustomerOutcome { get; set; }

        public bool Released { get; set; }

        public bool Settled { get; set; }
    }

    public sealed record MockRouteAttempt(string PaymentIntentId, string RouteCode, string Result);
}
