using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class SessionLifecycleTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task S24_One_payment_session_per_payable_instruction_while_active()
    {
        var first = await _harness.SendAsync(CreateCommand("create-1"));

        var retried = await _harness.SendAsync(CreateCommand("create-2"));

        Assert.Equal(first.Session.Id, retried.Session.Id);
        Assert.Single(_harness.Sessions.Committed);
        await AssertRejectedAsync(8021, () => _harness.SendAsync(CreateCommand("create-3", amount: Amount + 1)));
    }

    [Fact]
    public async Task S25_A_cancelled_or_expired_session_allows_a_fresh_session()
    {
        var cancelled = await _harness.SendAsync(CreateCommand("create-1"));
        await _harness.CancelAsync(cancelled.Session.Id);

        var afterCancel = await _harness.SendAsync(CreateCommand("create-2"));

        var expiring = await _harness.SendAsync(CreateCommand("create-3", orderId: 6001, expiresAt: _harness.Clock.Now.AddMinutes(10)));
        _harness.Clock.Advance(TimeSpan.FromMinutes(11));
        await _harness.ExpireDueAsync();
        var afterExpiry = await _harness.SendAsync(CreateCommand("create-4", orderId: 6001, expiresAt: _harness.Clock.Now.AddMinutes(10)));

        Assert.NotEqual(cancelled.Session.Id, afterCancel.Session.Id);
        Assert.Equal(PaymentSessionStatus.Expired, (await _harness.GetAsync(expiring.Session.Id)).Session.Status);
        Assert.NotEqual(expiring.Session.Id, afterExpiry.Session.Id);
    }

    [Fact]
    public async Task S31_Session_expiry_releases_non_captured_commitments()
    {
        var session = await StaffCompositionAsync(_harness.Clock.Now.AddMinutes(30));

        _harness.Clock.Advance(TimeSpan.FromMinutes(31));
        await _harness.ExpireDueAsync();
        var expired = await _harness.GetAsync(session.Session.Id);

        Assert.Equal(PaymentSessionStatus.Expired, expired.Session.Status);
        Assert.Equal(0, expired.Session.GuaranteedAmount);
        Assert.Equal(600_000m, expired.Session.CapturedAmount);
        Assert.Equal(PaymentIntentStatus.Expired, Leg(expired, TenderType.AgencyCredit).Status);
        Assert.Equal(PaymentIntentStatus.Captured, Leg(expired, TenderType.StoredValue).Status);
        Assert.Equal(PaymentIntentStatus.Captured, Leg(expired, TenderType.Cash).Status);
        Assert.Equal(0, _harness.Ledger.FacilitiesOf(PayerType.Agency, AgencyId, Irr).Single().Reserved);
    }

    [Fact]
    public async Task S32_Cancelling_never_silently_undoes_captured_money()
    {
        var session = await StaffCompositionAsync(null);

        var cancelled = await _harness.CancelAsync(session.Session.Id);

        Assert.Equal(PaymentSessionStatus.Cancelled, cancelled.Session.Status);
        Assert.Equal(600_000m, cancelled.Session.CapturedAmount);
        Assert.Equal(PaymentIntentStatus.Captured, Leg(cancelled, TenderType.StoredValue).Status);
        Assert.Equal(PaymentIntentStatus.Captured, Leg(cancelled, TenderType.Cash).Status);
        Assert.Equal(PaymentIntentStatus.Cancelled, Leg(cancelled, TenderType.AgencyCredit).Status);
        Assert.Equal(0, _harness.Ledger.FacilitiesOf(PayerType.Agency, AgencyId, Irr).Single().Reserved);
        Assert.Equal(0, _harness.Ledger.WalletsOf(PayerType.Agency, AgencyId, Irr).Single().Balance);
    }

    [Fact]
    public async Task S33_Late_capture_for_a_superseded_payable_instruction_is_paid_unapplied()
    {
        var superseded = (await _harness.CreateAsync(commercialVersion: 1)).Session;
        var leg = Leg(await _harness.FundAsync(superseded, null, (TenderType.IranianPgw, Amount)), TenderType.IranianPgw);
        var current = (await _harness.CreateAsync(commercialVersion: 2)).Session;

        var late = await _harness.CompleteAsync(leg.Id);

        Assert.Equal(PaymentIntentStatus.Captured, Leg(late, TenderType.IranianPgw).Status);
        Assert.Equal(0, Leg(late, TenderType.IranianPgw).GuaranteedAmount);
        Assert.Equal(Amount, late.Session.CapturedAmount);
        Assert.Equal(0, late.Session.GuaranteedAmount);

        var unapplied = Assert.Single(_harness.PaidUnapplied());
        Assert.Equal((superseded.Id, leg.Id, 5001L), (unapplied.PaymentSessionId, unapplied.PaymentIntentId, unapplied.OrderId));
        Assert.Equal(superseded.PayableInstructionId, unapplied.SupersededPayableInstructionId);
        Assert.Equal((Amount, Irr, PaidUnappliedReason.PayableInstructionSuperseded), (unapplied.CapturedAmount, unapplied.CurrencyId, unapplied.ReasonCode));
        Assert.Equal(PaymentSessionStatus.Created, (await _harness.GetAsync(current.Id)).Session.Status);
    }

    [Fact]
    public async Task S33_Money_captured_after_cancellation_is_kept_as_paid_unapplied()
    {
        var session = (await _harness.CreateAsync()).Session;
        var leg = Leg(await _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount)), TenderType.IranianPgw);
        _harness.Ledger.SetCustomerOutcome(leg.Id, MockCustomerOutcome.Approved);
        await _harness.CancelAsync(session.Id);

        var late = await _harness.ReconcileAsync(leg.Id);

        Assert.Equal(PaymentSessionStatus.Cancelled, late.Session.Status);
        Assert.Equal(Amount, late.Session.CapturedAmount);
        Assert.Equal(0, late.Session.GuaranteedAmount);
        Assert.Equal(PaidUnappliedReason.PaymentSessionCancelled, Assert.Single(_harness.PaidUnapplied()).ReasonCode);
    }

    [Fact]
    public async Task S39_A_zero_amount_payable_does_not_create_a_payment_session()
    {
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _harness.SendAsync(CreateCommand("zero", amount: 0)));

        Assert.Empty(_harness.Sessions.Committed);
    }

    private async Task<PaymentSessionResponse> StaffCompositionAsync(DateTimeOffset? expiresAt)
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 300_000m);
        _harness.Credit(TenderType.AgencyCredit, PayerType.Agency, AgencyId, 400_000m);
        _harness.AcceptCashAt();
        var session = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted, expiresAt: expiresAt)).Session;

        var covered = await _harness.FundAsync(session, null, (TenderType.StoredValue, 300_000m), (TenderType.AgencyCredit, 400_000m), (TenderType.Cash, 300_000m));
        Assert.Equal(PaymentSessionStatus.Guaranteed, covered.Session.Status);

        return covered;
    }

    public void Dispose() => _harness.Dispose();
}
