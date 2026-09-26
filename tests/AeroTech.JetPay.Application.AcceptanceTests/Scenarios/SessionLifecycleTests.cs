using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class SessionLifecycleTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Theory]
    [InlineData(PaymentInteractionMode.CustomerInteractive, PaymentSelectionMode.Interactive)]
    [InlineData(PaymentInteractionMode.UnattendedApi, PaymentSelectionMode.Default)]
    [InlineData(PaymentInteractionMode.StaffAssisted, PaymentSelectionMode.Explicit)]
    public async Task A15_A_zero_amount_session_is_paid_immediately_with_no_intent_or_attempt(PaymentInteractionMode interactionMode, PaymentSelectionMode selectionMode)
    {
        var session = await _harness.CreateAsync(PayerType.Agency, AgencyId, interactionMode, selectionMode, amount: 0m);

        Assert.Equal(PaymentSessionStatus.Paid, session.Status);
        Assert.Equal((0m, 0m, 0m), (session.RequiredAmount, session.CapturedAmount, session.OutstandingAmount));
        Assert.Empty(session.Intents);
        Assert.Empty(_harness.Ledger.RouteAttempts);
        Assert.Equal([PaymentSessionStatus.Paid], _harness.ChangesOf(session.Id).Select(change => change.Status));
    }

    [Fact]
    public async Task An_interactive_session_waits_for_a_method_and_an_explicit_one_without_selections_is_created()
    {
        var interactive = await _harness.CreateAsync();
        var explicitWithoutSelections = await _harness.CreateAsync(interactionMode: PaymentInteractionMode.StaffAssisted, orderId: 6001);

        Assert.Equal((PaymentSessionStatus.RequiresPaymentMethod, Amount), (interactive.Status, interactive.OutstandingAmount));
        Assert.Equal(PaymentSessionStatus.Created, explicitWithoutSelections.Status);
    }

    [Fact]
    public async Task A_back_office_explicit_create_funds_the_customer_order_from_the_selected_wallet()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 3_000_000m);
        var options = await _harness.ResolveAsync(interactionMode: PaymentInteractionMode.StaffAssisted);

        var session = await _harness.CreateAsync(
            interactionMode: PaymentInteractionMode.StaffAssisted,
            selections: [new PaymentSelection(OptionOf(options, TenderType.StoredValue), Amount)]);

        Assert.Equal(PaymentSessionStatus.Paid, session.Status);
        Assert.Equal(BackOffice, session.Initiator);
        Assert.Equal(TenderType.StoredValue, session.Intents.Single().TenderType);
    }

    [Fact]
    public async Task Stage_a_takes_one_selection_covering_the_outstanding_amount()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 3_000_000m);
        var session = await _harness.CreateAsync();
        var options = await _harness.OptionsFor(session);
        var wallet = OptionOf(options, TenderType.StoredValue);
        var pgw = OptionOf(options, TenderType.IranianPgw);

        await AssertRejectedAsync(8038, () => _harness.SelectAsync(session.Id, [new(wallet, 400_000m), new(pgw, 600_000m)]));
        await AssertRejectedAsync(8034, () => _harness.SelectAsync(session.Id, [new(pgw, 600_000m)]));
        await AssertRejectedAsync(8030, () => _harness.SelectAsync(session.Id, [new("opt_unknown", Amount)]));
        await AssertRejectedAsync(8031, () => _harness.SelectAsync(session.Id, []));
        await AssertRejectedAsync(8032, () => _harness.CreateAsync(orderId: 6001, selections: [new(pgw, Amount)]));
    }

    [Fact]
    public async Task A_wallet_that_cannot_cover_the_amount_is_rejected_at_selection()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 300_000m);
        var session = await _harness.CreateAsync();
        var options = await _harness.OptionsFor(session);

        await AssertRejectedAsync(8035, () => _harness.SelectAsync(session.Id, [new(OptionOf(options, TenderType.StoredValue), Amount)]));
    }

    [Fact]
    public async Task A16_A_cancelled_session_accepts_no_new_funding()
    {
        var session = await _harness.CreateAsync();
        var options = await _harness.OptionsFor(session);

        var cancelled = await _harness.CancelAsync(session.Id);

        Assert.Equal(PaymentSessionStatus.Cancelled, cancelled.Status);
        await AssertRejectedAsync(8001, () => _harness.SelectAsync(session.Id, [new(OptionOf(options, TenderType.IranianPgw), Amount)]));
    }

    [Fact]
    public async Task A16_An_expired_session_accepts_no_new_funding_before_or_after_the_sweep()
    {
        var session = await _harness.CreateAsync(expiresAt: _harness.Clock.Now.AddMinutes(30));
        var options = await _harness.OptionsFor(session);
        var selection = new PaymentSelection(OptionOf(options, TenderType.IranianPgw), Amount);
        _harness.Clock.Advance(TimeSpan.FromMinutes(31));

        await AssertRejectedAsync(8003, () => _harness.SelectAsync(session.Id, [selection]));

        await _harness.SweepAsync();

        Assert.Equal(PaymentSessionStatus.Expired, (await _harness.GetAsync(session.Id)).Status);
        await AssertRejectedAsync(8001, () => _harness.SelectAsync(session.Id, [selection]));
    }

    [Fact]
    public async Task Cancelling_a_pending_pgw_closes_the_intent_and_a_later_payment_is_auto_reversed()
    {
        var (session, intent) = await _harness.StartPgwAsync();

        var cancelled = await _harness.CancelAsync(session.Id);
        Assert.Equal(PaymentIntentStatus.Cancelled, cancelled.Intents.Single().Status);

        _harness.PayAtGateway(intent.Id);
        var afterCallback = await _harness.CallbackAsync(intent.Id);

        Assert.Equal(PaymentSessionStatus.Cancelled, afterCallback.Status);
        Assert.Equal(0m, afterCallback.CapturedAmount);
        Assert.Equal(ProviderPaymentAttemptStatus.AutoReversalPending, Assert.Single(_harness.AttemptsOf(intent.Id)).Status);
        Assert.Empty(_harness.PaidUnapplied());
    }

    [Fact]
    public async Task Cancelling_a_paid_session_keeps_the_money_and_reports_it_as_paid_unapplied_once()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 2_000_000m, isDefault: true);
        var session = await _harness.CreateAgencyDefaultAsync();

        var cancelled = await _harness.CancelAsync(session.Id, "cancel-1");
        await _harness.CancelAsync(session.Id, "cancel-1");
        await _harness.CancelAsync(session.Id, "cancel-2");

        Assert.Equal((PaymentSessionStatus.Cancelled, Amount, 0m), (cancelled.Status, cancelled.CapturedAmount, cancelled.GuaranteedAmount));
        var unapplied = Assert.Single(_harness.PaidUnapplied());
        Assert.Equal((PaidUnappliedReason.PaymentSessionCancelled, Amount), (unapplied.ReasonCode, unapplied.CapturedAmount));
    }

    [Fact]
    public async Task An_expired_session_releases_its_payable_instruction_for_a_new_session()
    {
        var first = await _harness.CreateAsync(expiresAt: _harness.Clock.Now.AddMinutes(5));
        _harness.Clock.Advance(TimeSpan.FromMinutes(6));
        await _harness.SweepAsync();

        var second = await _harness.CreateAsync();

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.PayableInstructionId, second.PayableInstructionId);
    }
}
