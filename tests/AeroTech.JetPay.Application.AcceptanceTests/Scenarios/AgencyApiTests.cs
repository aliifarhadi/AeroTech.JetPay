using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class AgencyApiTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A12_Default_irr_wallet_with_enough_balance_pays_the_session_with_no_customer_action()
    {
        var wallet = _harness.Wallet(PayerType.Agency, AgencyId, 2_500_000m, isDefault: true);

        var session = await _harness.CreateAgencyDefaultAsync();

        Assert.Equal(PaymentSessionStatus.Paid, session.Status);
        Assert.Equal(PaymentSelectionMode.Default, session.SelectionMode);
        Assert.Equal((Amount, 0m), (session.CapturedAmount, session.OutstandingAmount));

        var intent = Assert.Single(session.Intents);
        Assert.Equal((TenderType.StoredValue, PaymentIntentStatus.Captured, Amount), (intent.TenderType, intent.Status, intent.CapturedAmount));
        Assert.Null(intent.NextAction);

        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal((MockFundingLedger.StoredValueProfile, ProviderPaymentAttemptStatus.Verified), (attempt.ProviderProfileId, attempt.Status));
        Assert.Equal(1_500_000m, _harness.Ledger.WalletsOf(PayerType.Agency, AgencyId, Irr).Single(candidate => candidate.Id == wallet.Id).Balance);
    }

    [Fact]
    public async Task A13_An_insufficient_default_wallet_never_falls_back_to_a_customer_redirect()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 400_000m, isDefault: true);

        var session = await _harness.CreateAgencyDefaultAsync();

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, session.Status);
        Assert.Equal(FundingFailureCode.InsufficientFunds, session.FailureCode);
        Assert.Empty(session.Intents);
        Assert.Empty(_harness.Ledger.RouteAttempts);
        Assert.Equal(400_000m, _harness.Ledger.WalletsOf(PayerType.Agency, AgencyId, Irr).Single().Balance);
    }

    [Fact]
    public async Task A13_Without_a_default_wallet_the_default_funding_source_is_unavailable()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 9_000_000m, isDefault: false);

        var session = await _harness.CreateAgencyDefaultAsync();

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, session.Status);
        Assert.Equal(FundingFailureCode.DefaultFundingSourceUnavailable, session.FailureCode);
        Assert.Empty(session.Intents);
    }

    [Fact]
    public async Task A_wallet_the_agency_refills_can_fund_the_session_through_an_explicit_selection()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 400_000m, isDefault: true);
        var session = await _harness.CreateAgencyDefaultAsync();
        _harness.Wallet(PayerType.Agency, AgencyId, 1_000_000m, isDefault: true);

        var paid = await _harness.SelectAsync(session, TenderType.StoredValue);

        Assert.Equal(PaymentSessionStatus.Paid, paid.Status);
        Assert.Null(paid.FailureCode);
    }
}
