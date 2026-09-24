using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.Messages.JetPay.Enums;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.PaymentIntents;

public sealed class PaidUnappliedTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task A_late_capture_for_a_superseded_instruction_is_reported_as_paid_unapplied()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.LateCapturedForSupersededInstruction);
        var superseded = await _harness.CreateAndConfirmAsync(TenderType.IranianPgw, orderId: 5001, commercialVersion: 1);
        var current = await _harness.CreateForAsync(TenderType.IranianPgw, orderId: 5001, commercialVersion: 2);

        var late = await _harness.CompleteCustomerActionAsync(superseded.Id);

        Assert.Equal(PaymentIntentStatus.Captured, late.Status);
        Assert.Equal(PaymentHarness.Amount, late.CapturedAmount);
        Assert.Equal(0, late.GuaranteedAmount);

        var unapplied = Assert.Single(_harness.PaidUnapplied());
        Assert.Equal(superseded.Id, unapplied.PaymentIntentId);
        Assert.Equal(5001, unapplied.OrderId);
        Assert.Equal(superseded.PayableInstructionId, unapplied.SupersededPayableInstructionId);
        Assert.Equal(PaymentHarness.Amount, unapplied.CapturedAmount);
        Assert.Equal(PaymentHarness.CurrencyId, unapplied.CurrencyId);
        Assert.Equal(PaidUnappliedReason.PayableInstructionSuperseded, unapplied.ReasonCode);
        Assert.False(string.IsNullOrWhiteSpace(unapplied.EventId));

        Assert.Equal(PaymentIntentStatus.Created, (await _harness.GetAsync(current.Id)).Status);
    }

    [Fact]
    public async Task Money_captured_after_cancellation_is_recorded_truthfully_but_never_as_guarantee()
    {
        var awaitingCustomer = await _harness.CreateAndConfirmAsync(TenderType.IranianPgw);
        await _harness.CancelAsync(awaitingCustomer.Id);

        var late = await _harness.CompleteCustomerActionAsync(awaitingCustomer.Id);

        Assert.Equal(PaymentIntentStatus.Captured, late.Status);
        Assert.Equal(PaymentHarness.Amount, late.CapturedAmount);
        Assert.Equal(0, late.GuaranteedAmount);
        Assert.Equal(PaidUnappliedReason.PaymentIntentCancelled, Assert.Single(_harness.PaidUnapplied()).ReasonCode);
    }

    [Fact]
    public async Task A_capture_for_the_current_instruction_is_not_paid_unapplied()
    {
        var awaitingCustomer = await _harness.CreateAndConfirmAsync(TenderType.IranianPgw);

        await _harness.CompleteCustomerActionAsync(awaitingCustomer.Id);

        Assert.Empty(_harness.PaidUnapplied());
    }

    public void Dispose() => _harness.Dispose();
}
