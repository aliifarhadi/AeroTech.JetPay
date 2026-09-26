using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class CoverageTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task S35_Session_guarantee_is_the_sum_of_valid_leg_guarantees_capped_at_the_required_amount()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 300_000m);
        _harness.Credit(TenderType.AgencyCredit, PayerType.Agency, AgencyId, 400_000m);
        _harness.AcceptCashAt();
        var session = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted)).Session;

        var covered = await _harness.FundAsync(session, null, (TenderType.StoredValue, 300_000m), (TenderType.AgencyCredit, 400_000m), (TenderType.Cash, 300_000m));

        Assert.Equal(covered.PaymentIntents.Sum(leg => leg.GuaranteedAmount), covered.Session.GuaranteedAmount);
        Assert.Equal(covered.Session.RequiredAmount, covered.Session.GuaranteedAmount);
    }

    [Fact]
    public async Task S36_Earliest_guarantee_expiry_is_the_first_lapse_that_breaks_full_coverage()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 600_000m);
        _harness.Credit(TenderType.AgencyCredit, PayerType.Agency, AgencyId, 400_000m, validitySeconds: 3600);
        var session = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted)).Session;

        var covered = await _harness.FundAsync(session, null, (TenderType.StoredValue, 600_000m), (TenderType.AgencyCredit, 400_000m));

        Assert.Equal(PaymentSessionStatus.Guaranteed, covered.Session.Status);
        Assert.Equal(_harness.Clock.Now.AddHours(1), covered.Session.EarliestGuaranteeExpiry);

        _harness.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        await _harness.ExpireDueAsync();
        var lapsed = await _harness.GetAsync(session.Id);

        Assert.Equal(PaymentSessionStatus.PartiallyCovered, lapsed.Session.Status);
        Assert.Equal(600_000m, lapsed.Session.GuaranteedAmount);
        Assert.Null(lapsed.Session.EarliestGuaranteeExpiry);
        Assert.Equal(PaymentIntentStatus.Expired, Leg(lapsed, TenderType.AgencyCredit).Status);
        Assert.Equal(0, _harness.Ledger.FacilitiesOf(PayerType.Agency, AgencyId, Irr).Single().Reserved);
    }

    [Fact]
    public async Task S36_Fully_received_funds_have_no_guarantee_expiry()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, Amount);
        var session = (await _harness.CreateAsync()).Session;

        var paid = await _harness.FundAsync(session, null, (TenderType.StoredValue, Amount));

        Assert.Equal(PaymentSessionStatus.Paid, paid.Session.Status);
        Assert.Null(paid.Session.EarliestGuaranteeExpiry);
    }

    [Fact]
    public async Task S37_Funds_received_requirement_rejects_commitment_only_coverage()
    {
        _harness.Credit(TenderType.AgencyCredit, PayerType.Agency, AgencyId, 5_000_000m);
        var session = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted, PaymentAssuranceRequirement.FundsReceived)).Session;
        var commitmentOptions = await _harness.ResolveAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted);

        Assert.DoesNotContain(await _harness.OptionsFor(session), option => option.AssuranceCapability == PaymentAssuranceCapability.CommitmentToPay);
        await AssertRejectedAsync(8030, () => _harness.ConfirmAsync(session.Id, [new PaymentSelection(OptionOf(commitmentOptions, TenderType.AgencyCredit), Amount)]));
        Assert.Equal(0, _harness.Ledger.FacilitiesOf(PayerType.Agency, AgencyId, Irr).Single().Reserved);
    }

    [Fact]
    public async Task S38_Issuance_guaranteed_accepts_provider_commitment_or_received_funds()
    {
        _harness.Credit(TenderType.AgencyCredit, PayerType.Agency, AgencyId, 5_000_000m);
        _harness.Wallet(PayerType.Agency, AgencyId, 5_000_000m);
        var viaCommitment = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted, orderId: 7001)).Session;
        var viaFunds = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted, orderId: 7002)).Session;

        var committed = await _harness.FundAsync(viaCommitment, null, (TenderType.AgencyCredit, Amount));
        var funded = await _harness.FundAsync(viaFunds, null, (TenderType.StoredValue, Amount));

        Assert.Equal((PaymentSessionStatus.Guaranteed, Amount, 0m), (committed.Session.Status, committed.Session.GuaranteedAmount, committed.Session.CapturedAmount));
        Assert.Equal((PaymentSessionStatus.Paid, Amount, Amount), (funded.Session.Status, funded.Session.GuaranteedAmount, funded.Session.CapturedAmount));
    }

    [Fact]
    public async Task Bnpl_guarantee_expiry_removes_it_from_coverage()
    {
        var session = (await _harness.CreateAsync()).Session;
        var awaiting = await _harness.FundAsync(session, null, (TenderType.Bnpl, Amount));
        await _harness.CompleteAsync(Leg(awaiting, TenderType.Bnpl).Id);

        _harness.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));
        await _harness.ExpireDueAsync();
        var lapsed = await _harness.GetAsync(session.Id);

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, lapsed.Session.Status);
        Assert.Equal(0, lapsed.Session.GuaranteedAmount);
        Assert.Equal(PaymentIntentStatus.Expired, Leg(lapsed, TenderType.Bnpl).Status);
        Assert.Equal(Amount, Leg(lapsed, TenderType.Bnpl).AuthorizedAmount);
    }

    public void Dispose() => _harness.Dispose();
}
