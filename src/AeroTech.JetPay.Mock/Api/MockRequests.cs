using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Mock.Api
{
    public sealed record SetWalletRequest(PayerType PayerType, long PayerId, int CurrencyId, string Code, decimal Balance, bool IsDefault);

    public sealed record ConfigureProviderProfileRequest(
        bool? Enabled,
        MockPgwMode? Mode,
        IReadOnlyList<int>? CurrencyIds,
        decimal? MinimumAmount,
        decimal? MaximumAmount,
        bool? SupportsInquiry,
        bool? RequiresSettlementAfterVerify,
        int? VerifyWindowSeconds);

    public sealed record CustomerPaymentRequest(MockCustomerOutcome Outcome = MockCustomerOutcome.Paid);

    public sealed record AdvanceClockRequest(int Seconds);

    public sealed record ArmTransportTimeoutRequest(int Count);
}
