using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.Messages.JetPay.Enums;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.PaymentIntents;

public sealed class IdempotencyTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task Create_with_the_same_key_and_payload_returns_the_original_intent()
    {
        var command = PaymentHarness.CreateCommand("order-5001-payment-1");

        var original = await _harness.SendAsync(command);
        var replay = await _harness.SendAsync(command);

        Assert.Equal(original.Id, replay.Id);
        Assert.Equal(original.Version, replay.Version);
        Assert.Single(_harness.Intents.Committed);
        Assert.Single(_harness.ChangesOf(original.Id));
    }

    [Fact]
    public async Task Create_replay_treats_equal_amounts_of_different_scale_as_the_same_payload()
    {
        var command = PaymentHarness.CreateCommand("order-5001-payment-1", amount: 1_250_000m);

        var original = await _harness.SendAsync(command);
        var replay = await _harness.SendAsync(command with { Amount = 1_250_000.00m });

        Assert.Equal(original.Id, replay.Id);
        Assert.Single(_harness.Intents.Committed);
    }

    [Fact]
    public async Task Create_key_reuse_with_a_changed_payload_is_rejected()
    {
        var command = PaymentHarness.CreateCommand("order-5001-payment-1");
        await _harness.SendAsync(command);

        await PaymentHarness.AssertRejectedAsync(8020, () => _harness.SendAsync(command with { Amount = command.Amount + 1 }));
        await PaymentHarness.AssertRejectedAsync(8020, () => _harness.SendAsync(command with { CommercialVersion = 2, PayableInstructionId = "payable-5001-v2" }));

        Assert.Single(_harness.Intents.Committed);
    }

    [Fact]
    public async Task A_new_key_for_the_same_payable_instruction_returns_the_active_intent_instead_of_a_second_one()
    {
        var original = await _harness.SendAsync(PaymentHarness.CreateCommand("first-attempt"));

        var retried = await _harness.SendAsync(PaymentHarness.CreateCommand("regenerated-key-after-timeout"));

        Assert.Equal(original.Id, retried.Id);
        Assert.Single(_harness.Intents.Committed);
    }

    [Fact]
    public async Task A_payable_instruction_bound_to_an_active_intent_cannot_change_its_terms()
    {
        await _harness.SendAsync(PaymentHarness.CreateCommand("first-attempt"));

        await PaymentHarness.AssertRejectedAsync(8021, () => _harness.SendAsync(
            PaymentHarness.CreateCommand("second-attempt", amount: PaymentHarness.Amount * 2)));
    }

    [Fact]
    public async Task A_failed_intent_releases_its_payable_instruction_for_a_new_intent()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.StoredValueInsufficientBalance);
        var failed = await _harness.CreateAndConfirmAsync(TenderType.StoredValue);

        var retry = await _harness.SendAsync(PaymentHarness.CreateCommand("retry-with-new-tender"));

        Assert.Equal(PaymentIntentStatus.Failed, failed.Status);
        Assert.NotEqual(failed.Id, retry.Id);
        Assert.Equal(failed.PayableInstructionId, retry.PayableInstructionId);
    }

    [Fact]
    public async Task Create_while_the_same_key_is_in_flight_is_rejected_without_side_effects()
    {
        _harness.Lock.Hold("jetpay:payment-intent-creation:in-flight-key");

        await PaymentHarness.AssertRejectedAsync(8002, () => _harness.SendAsync(PaymentHarness.CreateCommand("in-flight-key")));

        Assert.Empty(_harness.Intents.Committed);
    }

    [Fact]
    public async Task Confirm_replay_returns_the_committed_state_without_a_second_provider_effect()
    {
        var intent = await _harness.CreateForAsync(TenderType.StoredValue);

        var confirmed = await _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.StoredValue), "confirm-1");
        var replay = await _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.StoredValue), "confirm-1");

        Assert.Equal(PaymentIntentStatus.Captured, replay.Status);
        Assert.Equal(confirmed.Version, replay.Version);
        Assert.Equal(PaymentHarness.Amount, replay.CapturedAmount);
        Assert.Equal(2, _harness.ChangesOf(intent.Id).Count);
    }

    [Fact]
    public async Task Confirm_key_reuse_with_a_changed_payload_is_rejected()
    {
        var intent = await _harness.CreateForAsync(TenderType.IranianPgw);
        await _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.IranianPgw), "confirm-1");

        await PaymentHarness.AssertRejectedAsync(8020, () => _harness.ConfirmAsync(
            intent.Id, PaymentHarness.OptionFor(TenderType.StoredValue), "confirm-1"));
    }

    [Fact]
    public async Task Confirm_under_a_new_key_after_a_financial_effect_is_rejected()
    {
        var intent = await _harness.CreateAndConfirmAsync(TenderType.StoredValue);

        await PaymentHarness.AssertRejectedAsync(8001, () => _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.StoredValue)));

        Assert.Equal(PaymentHarness.Amount, (await _harness.GetAsync(intent.Id)).CapturedAmount);
    }

    [Fact]
    public async Task Capture_replay_never_captures_twice()
    {
        var authorized = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);

        await _harness.CaptureAsync(authorized.Id, idempotencyKey: "capture-1");
        var replay = await _harness.CaptureAsync(authorized.Id, idempotencyKey: "capture-1");

        Assert.Equal(PaymentIntentStatus.Captured, replay.Status);
        Assert.Equal(PaymentHarness.Amount, replay.CapturedAmount);
        Assert.Single(_harness.ChangesOf(authorized.Id), change => change.Status == PaymentIntentStatus.Captured);
        await PaymentHarness.AssertRejectedAsync(8020, () => _harness.CaptureAsync(authorized.Id, amount: 1, idempotencyKey: "capture-1"));
    }

    [Fact]
    public async Task Cancel_replay_is_answered_from_the_committed_state()
    {
        var authorized = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);

        await _harness.CancelAsync(authorized.Id, idempotencyKey: "cancel-1");
        var replay = await _harness.CancelAsync(authorized.Id, idempotencyKey: "cancel-1");

        Assert.Equal(PaymentIntentStatus.Cancelled, replay.Status);
        Assert.Single(_harness.ChangesOf(authorized.Id), change => change.Status == PaymentIntentStatus.Cancelled);
        await PaymentHarness.AssertRejectedAsync(8020, () => _harness.CancelAsync(
            authorized.Id, PaymentCancellationReason.OrderCancelled, "cancel-1"));
    }

    [Fact]
    public async Task Mutations_without_an_idempotency_key_are_rejected_by_validation()
    {
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _harness.SendAsync(PaymentHarness.CreateCommand(string.Empty)));
    }

    public void Dispose() => _harness.Dispose();
}
