using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.Messages.JetPay.Enums;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.PaymentIntents;

public sealed class ExpiryTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task An_expired_guarantee_no_longer_contributes_a_guaranteed_amount()
    {
        var intent = await _harness.CreateAndConfirmAsync(TenderType.Bnpl);
        var authorized = await _harness.CompleteCustomerActionAsync(intent.Id);

        _harness.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));
        var expiredCount = await _harness.ExpireDueAsync();
        var expired = await _harness.GetAsync(intent.Id);

        Assert.Equal(1, expiredCount);
        Assert.Equal(PaymentIntentStatus.Expired, expired.Status);
        Assert.Equal(0, expired.GuaranteedAmount);
        Assert.Equal(authorized.AuthorizedAmount, expired.AuthorizedAmount);
        Assert.Equal(authorized.GuaranteeExpiresAt, expired.GuaranteeExpiresAt);
        Assert.Equal(0, _harness.ChangesOf(intent.Id)[^1].GuaranteedAmount);
    }

    [Fact]
    public async Task A_guarantee_is_not_expired_before_its_expiry()
    {
        var intent = await _harness.CreateAndConfirmAsync(TenderType.Bnpl);
        await _harness.CompleteCustomerActionAsync(intent.Id);

        _harness.Clock.Advance(TimeSpan.FromHours(23));

        Assert.Equal(0, await _harness.ExpireDueAsync());
        Assert.Equal(PaymentIntentStatus.Authorized, (await _harness.GetAsync(intent.Id)).Status);
    }

    [Fact]
    public async Task Capturing_after_the_guarantee_expired_expires_the_intent_instead()
    {
        var intent = await _harness.CreateAndConfirmAsync(TenderType.Bnpl);
        await _harness.CompleteCustomerActionAsync(intent.Id);
        _harness.Clock.Advance(TimeSpan.FromDays(2));

        await PaymentHarness.AssertRejectedAsync(8003, () => _harness.CaptureAsync(intent.Id));

        var readBack = await _harness.GetAsync(intent.Id);
        Assert.Equal(PaymentIntentStatus.Expired, readBack.Status);
        Assert.Equal(0, readBack.CapturedAmount);
    }

    [Fact]
    public async Task A_guarantee_without_a_known_expiry_does_not_lapse()
    {
        var authorized = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);

        _harness.Clock.Advance(TimeSpan.FromDays(30));

        Assert.Null(authorized.GuaranteeExpiresAt);
        Assert.Equal(0, await _harness.ExpireDueAsync());
        Assert.Equal(PaymentHarness.Amount, (await _harness.GetAsync(authorized.Id)).GuaranteedAmount);
    }

    [Theory]
    [InlineData(TenderType.IranianPgw)]
    [InlineData(TenderType.Bnpl)]
    public async Task PaymentExpired_lets_an_unfinished_customer_action_expire(TenderType tender)
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.PaymentExpired);
        var awaitingCustomer = await _harness.CreateAndConfirmAsync(tender);

        _harness.Clock.Advance(TimeSpan.FromSeconds(301));
        await _harness.ExpireDueAsync();
        var expired = await _harness.GetAsync(awaitingCustomer.Id);

        Assert.Equal(_harness.Clock.Now.AddSeconds(-1), awaitingCustomer.NextAction!.ExpiresAt);
        Assert.Equal(PaymentIntentStatus.Expired, expired.Status);
        Assert.Null(expired.NextAction);
        Assert.Equal(0, expired.GuaranteedAmount);
    }

    [Fact]
    public async Task PaymentExpired_lets_an_agency_credit_authorization_lapse()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.PaymentExpired);
        var authorized = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);

        _harness.Clock.Advance(TimeSpan.FromSeconds(301));
        await _harness.ExpireDueAsync();

        Assert.Equal(PaymentIntentStatus.Authorized, authorized.Status);
        Assert.Equal(PaymentIntentStatus.Expired, (await _harness.GetAsync(authorized.Id)).Status);
    }

    [Fact]
    public async Task An_unconfirmed_intent_expires_at_its_intent_expiry()
    {
        var intent = await _harness.CreateForAsync(TenderType.IranianPgw, intentExpiresAt: _harness.Clock.Now.AddMinutes(10));

        _harness.Clock.Advance(TimeSpan.FromMinutes(11));

        await PaymentHarness.AssertRejectedAsync(8003, () => _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.IranianPgw)));
        Assert.Equal(PaymentIntentStatus.Expired, (await _harness.GetAsync(intent.Id)).Status);
    }

    [Fact]
    public async Task A_redirect_never_outlives_the_intent_expiry()
    {
        var intent = await _harness.CreateForAsync(TenderType.IranianPgw, intentExpiresAt: _harness.Clock.Now.AddMinutes(5));

        var awaitingCustomer = await _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.IranianPgw));

        Assert.Equal(intent.IntentExpiresAt, awaitingCustomer.NextAction!.ExpiresAt);
    }

    [Fact]
    public async Task An_intent_expiry_in_the_past_is_rejected()
        => await PaymentHarness.AssertRejectedAsync(8009, () => _harness.CreateForAsync(TenderType.StoredValue, intentExpiresAt: _harness.Clock.Now));

    public void Dispose() => _harness.Dispose();
}
