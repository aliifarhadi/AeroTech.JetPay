using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class AgencyApiTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task S12_Default_mode_debits_the_default_wallet_without_customer_action()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 5_000_000m, isDefault: true);
        var session = (await AgencySessionAsync(PaymentAssuranceRequirement.FundsReceived)).Session;

        var paid = await _harness.ConfirmDefaultAsync(session.Id);

        var leg = Assert.Single(paid.PaymentIntents);
        Assert.Equal(PaymentSessionStatus.Paid, paid.Session.Status);
        Assert.Equal((TenderType.StoredValue, PaymentIntentStatus.Captured), (leg.TenderType, leg.Status));
        Assert.Null(leg.NextAction);
        Assert.DoesNotContain(_harness.ChangesOf(session.Id), change => change.Status == PaymentSessionStatus.RequiresCustomerAction);
        Assert.Equal(4_000_000m, _harness.Ledger.WalletsOf(PayerType.Agency, AgencyId, Irr).Single().Balance);
        Assert.Empty(_harness.Ledger.RouteAttempts);
    }

    [Fact]
    public async Task S13_Insufficient_default_wallet_fails_deterministically_without_pgw_fallback()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 100_000m, isDefault: true);
        var session = (await AgencySessionAsync(PaymentAssuranceRequirement.FundsReceived)).Session;

        var result = await _harness.ConfirmDefaultAsync(session.Id);

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, result.Session.Status);
        Assert.Equal(FundingFailureCode.InsufficientFunds, result.Session.FailureCode);
        Assert.Empty(result.PaymentIntents);
        Assert.Empty(_harness.Ledger.RouteAttempts);
        Assert.Empty(_harness.OperationsOf(MockOperationKind.PgwTransaction));
        Assert.Equal(100_000m, _harness.Ledger.WalletsOf(PayerType.Agency, AgencyId, Irr).Single().Balance);
    }

    [Fact]
    public async Task S13_Missing_default_wallet_fails_deterministically()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 5_000_000m);
        var session = (await AgencySessionAsync(PaymentAssuranceRequirement.FundsReceived)).Session;

        var result = await _harness.ConfirmDefaultAsync(session.Id);

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, result.Session.Status);
        Assert.Equal(FundingFailureCode.DefaultFundingSourceUnavailable, result.Session.FailureCode);
        Assert.Empty(result.PaymentIntents);
    }

    [Fact]
    public async Task S14_Agency_explicitly_selects_another_eligible_wallet()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 100_000m, isDefault: true, code: "main");
        _harness.Wallet(PayerType.Agency, AgencyId, 2_000_000m, code: "promo");
        var session = (await AgencySessionAsync(PaymentAssuranceRequirement.FundsReceived)).Session;
        var promo = (await _harness.OptionsFor(session)).Single(option => option is { TenderType: TenderType.StoredValue, IsDefault: false });

        var paid = await _harness.ConfirmAsync(session.Id, [new PaymentSelection(promo.Id, Amount)]);

        Assert.Equal(PaymentSessionStatus.Paid, paid.Session.Status);
        var wallets = _harness.Ledger.WalletsOf(PayerType.Agency, AgencyId, Irr).ToDictionary(wallet => wallet.Code, wallet => wallet.Balance);
        Assert.Equal(100_000m, wallets["main"]);
        Assert.Equal(1_000_000m, wallets["promo"]);
    }

    [Fact]
    public async Task S15_Agency_credit_guarantees_with_zero_captured_money()
    {
        _harness.Credit(TenderType.AgencyCredit, PayerType.Agency, AgencyId, 5_000_000m);
        var session = (await AgencySessionAsync(PaymentAssuranceRequirement.IssuanceGuaranteed)).Session;

        var guaranteed = await _harness.FundAsync(session, null, (TenderType.AgencyCredit, Amount));
        var leg = Leg(guaranteed, TenderType.AgencyCredit);

        Assert.Equal(PaymentSessionStatus.Guaranteed, guaranteed.Session.Status);
        Assert.Equal(0, guaranteed.Session.CapturedAmount);
        Assert.Equal((PaymentIntentStatus.Authorized, PaymentCaptureMode.Manual, Amount, 0m), (leg.Status, leg.CaptureMode, leg.GuaranteedAmount, leg.CapturedAmount));
        Assert.Equal(Amount, _harness.Ledger.FacilitiesOf(PayerType.Agency, AgencyId, Irr).Single().Reserved);
    }

    private Task<PaymentSessionAggregate.Views.PaymentSessionResponse> AgencySessionAsync(PaymentAssuranceRequirement assurance)
        => _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.UnattendedApi, assurance);

    public void Dispose() => _harness.Dispose();
}
