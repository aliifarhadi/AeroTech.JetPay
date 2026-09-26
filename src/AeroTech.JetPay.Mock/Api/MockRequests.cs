using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.JetPay.Mock.Api
{
    public sealed record SetWalletRequest(PayerType PayerType, long PayerId, int CurrencyId, string Code, decimal Balance, bool IsDefault);

    public sealed record SetCreditFacilityRequest(TenderType TenderType, PayerType PayerType, long PayerId, int CurrencyId, decimal Limit, int? AuthorizationValiditySeconds);

    public sealed record SetCashAcceptanceRequest(IReadOnlyList<long> OfficeIds, IReadOnlyList<SalesChannel>? SalesChannels);

    public sealed record PayerReference(PayerType PayerType, long PayerId);

    public sealed record SetBnplRequest(bool Enabled, decimal? MinimumAmount, decimal? MaximumAmount, IReadOnlyList<PayerReference>? DisabledPayers);

    public sealed record ConfigurePgwProfileRequest(
        int? Priority,
        bool? Enabled,
        IReadOnlyList<int>? CurrencyIds,
        MockPgwMode? Mode,
        MockInquiryOutcome? InquiryOutcome,
        decimal? MinimumAmount,
        decimal? MaximumAmount);

    public sealed record CompleteCustomerActionRequest(MockCustomerOutcome Outcome = MockCustomerOutcome.Approved);

    public sealed record AdvanceClockRequest(int Seconds);

    public sealed record ArmTransportTimeoutRequest(int Count);
}
