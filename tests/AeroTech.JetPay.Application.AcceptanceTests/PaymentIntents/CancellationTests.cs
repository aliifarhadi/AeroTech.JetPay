using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.Messages.JetPay.Enums;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.PaymentIntents;

public sealed class CancellationTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task Cancelling_an_authorized_intent_releases_its_guarantee()
    {
        var authorized = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);

        var cancelled = await _harness.CancelAsync(authorized.Id);

        Assert.Equal(PaymentIntentStatus.Cancelled, cancelled.Status);
        Assert.Equal(0, cancelled.GuaranteedAmount);
        Assert.Null(cancelled.GuaranteeExpiresAt);
        Assert.Equal(0, cancelled.CapturedAmount);
    }

    [Fact]
    public async Task Cancelling_an_intent_awaiting_the_customer_closes_the_redirect()
    {
        var awaitingCustomer = await _harness.CreateAndConfirmAsync(TenderType.IranianPgw);

        var cancelled = await _harness.CancelAsync(awaitingCustomer.Id);

        Assert.Equal(PaymentIntentStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.NextAction);
    }

    [Fact]
    public async Task Cancelling_an_unconfirmed_intent_needs_no_provider()
    {
        var intent = await _harness.CreateForAsync(TenderType.StoredValue);

        var cancelled = await _harness.CancelAsync(intent.Id, PaymentCancellationReason.CommercialVersionSuperseded);

        Assert.Equal(PaymentIntentStatus.Cancelled, cancelled.Status);
    }

    [Theory]
    [InlineData(TenderType.StoredValue)]
    [InlineData(TenderType.AgencyCredit)]
    public async Task Captured_money_cannot_be_cancelled_as_if_it_had_not_moved(TenderType tender)
    {
        var intent = await _harness.CreateAndConfirmAsync(tender);

        if (intent.Status == PaymentIntentStatus.Authorized)
            await _harness.CaptureAsync(intent.Id, amount: 1_000m, finalCapture: false);

        await PaymentHarness.AssertRejectedAsync(8005, () => _harness.CancelAsync(intent.Id));

        var readBack = await _harness.GetAsync(intent.Id);
        Assert.NotEqual(PaymentIntentStatus.Cancelled, readBack.Status);
        Assert.True(readBack.CapturedAmount > 0);
    }

    [Fact]
    public async Task An_intent_with_a_pending_provider_outcome_cannot_be_cancelled()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.PaymentProcessing);
        var processing = await _harness.CreateAndConfirmAsync(TenderType.StoredValue);

        await PaymentHarness.AssertRejectedAsync(8004, () => _harness.CancelAsync(processing.Id));
    }

    [Fact]
    public async Task Cancelling_again_under_a_new_key_changes_nothing()
    {
        var authorized = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);
        var cancelled = await _harness.CancelAsync(authorized.Id);

        var again = await _harness.CancelAsync(authorized.Id);

        Assert.Equal(cancelled.Version, again.Version);
        Assert.Single(_harness.ChangesOf(authorized.Id), change => change.Status == PaymentIntentStatus.Cancelled);
    }

    [Fact]
    public async Task A_failed_intent_cannot_be_cancelled()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.StoredValueInsufficientBalance);
        var failed = await _harness.CreateAndConfirmAsync(TenderType.StoredValue);

        await PaymentHarness.AssertRejectedAsync(8001, () => _harness.CancelAsync(failed.Id));
    }

    public void Dispose() => _harness.Dispose();
}
