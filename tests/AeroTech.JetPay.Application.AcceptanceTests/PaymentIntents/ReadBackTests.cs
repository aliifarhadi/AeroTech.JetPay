using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.Messages.JetPay.Enums;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.PaymentIntents;

public sealed class ReadBackTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task Get_returns_the_latest_committed_state()
    {
        var intent = await _harness.CreateAndConfirmAsync(TenderType.IranianPgw);
        var completed = await _harness.CompleteCustomerActionAsync(intent.Id);

        var readBack = await _harness.GetAsync(intent.Id);

        Assert.Equal(completed.Status, readBack.Status);
        Assert.Equal(completed.Version, readBack.Version);
        Assert.Equal(_harness.ChangesOf(intent.Id)[^1].Version, readBack.Version);
        Assert.Equal(PaymentIntentStatus.Captured, readBack.Status);
    }

    [Fact]
    public async Task A_lost_confirm_response_is_recovered_by_read_back_and_a_same_key_retry()
    {
        var intent = await _harness.CreateForAsync(TenderType.StoredValue);

        // The confirm commits, but the caller never receives the response.
        _ = await _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.StoredValue), "confirm-after-timeout");

        var readBack = await _harness.GetAsync(intent.Id);
        var retry = await _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.StoredValue), "confirm-after-timeout");

        Assert.Equal(PaymentIntentStatus.Captured, readBack.Status);
        Assert.Equal(readBack.Version, retry.Version);
        Assert.Equal(PaymentHarness.Amount, retry.CapturedAmount);
        Assert.Single(_harness.ChangesOf(intent.Id), change => change.Status == PaymentIntentStatus.Captured);
    }

    [Fact]
    public void There_is_no_unknown_business_status()
        => Assert.DoesNotContain("Unknown", Enum.GetNames<PaymentIntentStatus>());

    [Fact]
    public async Task A_missing_intent_is_reported_as_not_found()
        => await PaymentHarness.AssertRejectedAsync(8000, () => _harness.GetAsync("does-not-exist"));

    public void Dispose() => _harness.Dispose();
}
