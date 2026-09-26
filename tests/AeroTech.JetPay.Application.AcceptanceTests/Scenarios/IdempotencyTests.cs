using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class IdempotencyTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task S26_The_same_create_key_replays_the_same_session_and_rejects_a_changed_payload()
    {
        var command = CreateCommand("order-5001-checkout");

        var original = await _harness.SendAsync(command);
        var replay = await _harness.SendAsync(command with { Amount = 1_000_000.00m });

        Assert.Equal(original.Session.Id, replay.Session.Id);
        Assert.Equal(original.Session.Version, replay.Session.Version);
        Assert.Single(_harness.Sessions.Committed);
        Assert.Single(_harness.ChangesOf(original.Session.Id));
        await AssertRejectedAsync(8020, () => _harness.SendAsync(command with { Amount = Amount + 1 }));
    }

    [Fact]
    public async Task S27_A_lost_create_response_is_recovered_by_replay_and_read_back()
    {
        var command = CreateCommand("order-5001-checkout");

        _ = await _harness.SendAsync(command);
        var replay = await _harness.SendAsync(command);
        var readBack = await _harness.GetAsync(replay.Session.Id);

        Assert.Single(_harness.Sessions.Committed);
        Assert.Equal(replay.Session.Id, readBack.Session.Id);
        Assert.Equal(PaymentSessionStatus.Created, readBack.Session.Status);
    }

    [Fact]
    public async Task S28_A_lost_confirm_response_is_recovered_without_a_second_wallet_debit()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 2_000_000m);
        var session = (await _harness.CreateAsync(assurance: PaymentAssuranceRequirement.FundsReceived)).Session;
        IReadOnlyList<PaymentSelection> plan = [new(OptionOf(await _harness.OptionsFor(session), TenderType.StoredValue), Amount)];

        _ = await _harness.ConfirmAsync(session.Id, plan, "confirm-1");
        var readBack = await _harness.GetAsync(session.Id);
        var replay = await _harness.ConfirmAsync(session.Id, plan, "confirm-1");

        Assert.Equal(PaymentSessionStatus.Paid, readBack.Session.Status);
        Assert.Equal(readBack.Session.Version, replay.Session.Version);
        Assert.Single(replay.PaymentIntents);
        Assert.Single(_harness.OperationsOf(MockOperationKind.WalletDebit));
        Assert.Equal(1_000_000m, _harness.Ledger.WalletsOf(PayerType.Customer, CustomerId, Irr).Single().Balance);
    }

    [Fact]
    public async Task A_changed_plan_under_the_same_confirm_key_is_rejected()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 2_000_000m);
        var session = (await _harness.CreateAsync()).Session;
        var options = await _harness.OptionsFor(session);
        await _harness.ConfirmAsync(session.Id, [new PaymentSelection(OptionOf(options, TenderType.StoredValue), Amount)], "confirm-1");

        await AssertRejectedAsync(8020, () => _harness.ConfirmAsync(session.Id, [new PaymentSelection(OptionOf(options, TenderType.IranianPgw), Amount)], "confirm-1"));

        Assert.Single(_harness.OperationsOf(MockOperationKind.WalletDebit));
    }

    [Fact]
    public async Task A_replayed_cash_confirm_never_records_a_second_receipt()
    {
        _harness.AcceptCashAt();
        var session = (await _harness.CreateAsync(interactionMode: PaymentInteractionMode.StaffAssisted)).Session;

        await _harness.FundAsync(session, "cash-1", (TenderType.Cash, Amount));
        var replay = await _harness.FundAsync(session, "cash-1", (TenderType.Cash, Amount));

        Assert.Equal(PaymentSessionStatus.Paid, replay.Session.Status);
        Assert.Single(_harness.OperationsOf(MockOperationKind.CashReceipt));
    }

    [Fact]
    public async Task A_replayed_credit_confirm_never_reserves_exposure_twice()
    {
        _harness.Credit(TenderType.AgencyCredit, PayerType.Agency, AgencyId, 5_000_000m);
        var session = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.UnattendedApi)).Session;

        await _harness.FundAsync(session, "credit-1", (TenderType.AgencyCredit, Amount));
        await _harness.FundAsync(session, "credit-1", (TenderType.AgencyCredit, Amount));

        Assert.Single(_harness.OperationsOf(MockOperationKind.CreditReservation));
        Assert.Equal(Amount, _harness.Ledger.FacilitiesOf(PayerType.Agency, AgencyId, Irr).Single().Reserved);
    }

    [Fact]
    public async Task A_crash_mid_plan_is_resumed_by_replay_without_repeating_finished_legs()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 300_000m);
        _harness.AcceptCashAt();
        var session = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted)).Session;
        var options = await _harness.OptionsFor(session);
        IReadOnlyList<PaymentSelection> plan =
        [
            new(OptionOf(options, TenderType.StoredValue), 300_000m),
            new(OptionOf(options, TenderType.Cash), 700_000m)
        ];
        _harness.CrashBeforeNextDispatchOf(TenderType.Cash);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _harness.ConfirmAsync(session.Id, plan, "plan-1"));
        var interrupted = await _harness.GetAsync(session.Id);
        var resumed = await _harness.ConfirmAsync(session.Id, plan, "plan-1");

        Assert.Equal(PaymentIntentStatus.Captured, Leg(interrupted, TenderType.StoredValue).Status);
        Assert.Equal(PaymentIntentStatus.Created, Leg(interrupted, TenderType.Cash).Status);
        Assert.Equal(PaymentSessionStatus.Processing, interrupted.Session.Status);
        Assert.Equal(PaymentSessionStatus.Paid, resumed.Session.Status);
        Assert.Equal(2, resumed.PaymentIntents.Count);
        Assert.Single(_harness.OperationsOf(MockOperationKind.WalletDebit));
        Assert.Single(_harness.OperationsOf(MockOperationKind.CashReceipt));
    }

    [Fact]
    public async Task Cancelling_after_a_crash_resolves_the_undispatched_leg_before_closing()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 300_000m);
        _harness.AcceptCashAt();
        var session = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted)).Session;
        _harness.CrashBeforeNextDispatchOf(TenderType.Cash);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _harness.FundAsync(session, "plan-1", (TenderType.StoredValue, 300_000m), (TenderType.Cash, 700_000m)));

        var cancelled = await _harness.CancelAsync(session.Id);

        Assert.Equal(PaymentSessionStatus.Cancelled, cancelled.Session.Status);
        Assert.Equal((PaymentIntentStatus.Failed, "NoProviderEffect"), (Leg(cancelled, TenderType.Cash).Status, Leg(cancelled, TenderType.Cash).FailureCode));
        Assert.Equal(PaymentIntentStatus.Captured, Leg(cancelled, TenderType.StoredValue).Status);
        Assert.Empty(_harness.OperationsOf(MockOperationKind.CashReceipt));
    }

    [Fact]
    public async Task A_session_busy_with_another_operation_rejects_a_concurrent_confirm()
    {
        var session = (await _harness.CreateAsync()).Session;
        _harness.Lock.Hold($"jetpay:payment-session:{session.Id}");

        await AssertRejectedAsync(8002, () => _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount)));

        Assert.Empty(_harness.Intents.Committed);
    }

    [Fact]
    public async Task Mutations_without_an_idempotency_key_are_rejected()
    {
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _harness.SendAsync(CreateCommand(string.Empty)));
    }

    public void Dispose() => _harness.Dispose();
}
