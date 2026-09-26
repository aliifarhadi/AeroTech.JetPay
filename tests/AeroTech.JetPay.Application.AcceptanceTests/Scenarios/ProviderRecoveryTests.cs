using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class ProviderRecoveryTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A08_A_lost_callback_is_recovered_by_inquiry_without_a_second_charge()
    {
        var (session, intent) = await _harness.StartPgwAsync();
        _harness.PayAtGateway(intent.Id);

        var reconciled = await _harness.ReconcileAsync(intent.Id);

        Assert.Equal(PaymentSessionStatus.Paid, reconciled.Status);
        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal(ProviderPaymentAttemptStatus.Settled, attempt.Status);
        Assert.Null(attempt.CallbackReceivedAt);
        Assert.NotNull(attempt.VerifyDeadline);

        var transaction = Assert.Single(_harness.Ledger.OperationsOf(intent.Id));
        Assert.Equal(1, transaction.StartCalls);

        await AssertRejectedAsync(8005, () => _harness.SelectAsync(session.Id, [new(intent.PaymentMethodOptionId, Amount)]));
    }

    [Fact]
    public async Task A09_Without_inquiry_an_unresolved_effect_blocks_any_other_route_until_the_auto_reversal_boundary()
    {
        _harness.ConfigureProfile(MockFundingLedger.PrimaryPgwProfile, profile => profile.Enabled = false);
        var (session, intent) = await _harness.StartPgwAsync();
        var actionExpiresAt = intent.NextAction!.ExpiresAt!.Value;
        var retry = new[] { new Application.PaymentSessionAggregate.Views.PaymentSelection(intent.PaymentMethodOptionId, Amount) };

        await AssertRejectedAsync(8007, () => _harness.SelectAsync(session.Id, retry));

        var unchanged = await _harness.ReconcileAsync(intent.Id);
        Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, unchanged.Intents.Single().Status);

        _harness.Clock.Now = actionExpiresAt.AddSeconds(1);
        await _harness.SweepAsync();

        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal(ProviderPaymentAttemptStatus.Unknown, attempt.Status);
        Assert.Equal(actionExpiresAt + VerifyWindow, attempt.ReversalExpectedAt);
        Assert.Equal(PaymentIntentStatus.Expired, (await _harness.GetAsync(session.Id)).Intents.Single().Status);

        await AssertRejectedAsync(8007, () => _harness.SelectAsync(session.Id, retry));

        _harness.Clock.Now = attempt.ReversalExpectedAt!.Value;
        await _harness.SweepAsync();

        Assert.Equal(ProviderPaymentAttemptStatus.Reversed, Assert.Single(_harness.AttemptsOf(intent.Id)).Status);

        var rerouted = await _harness.SelectAsync(session.Id, retry);
        Assert.Equal(2, rerouted.Intents.Count);
        Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, rerouted.Intents.Last().Status);
    }

    [Fact]
    public async Task A09_An_unknown_start_without_inquiry_stays_blocked_until_the_provider_would_have_returned_the_money()
    {
        _harness.ConfigureProfile(MockFundingLedger.PrimaryPgwProfile, profile => profile.Enabled = false);
        _harness.ConfigureProfile(MockFundingLedger.SecondaryPgwProfile, profile => profile.Mode = MockPgwMode.UnknownAfterEffect);
        var startedAt = _harness.Clock.Now;

        var (session, intent) = await _harness.StartPgwAsync();

        Assert.Equal(PaymentSessionStatus.Processing, session.Status);
        Assert.Equal(PaymentIntentStatus.Processing, intent.Status);
        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal((ProviderPaymentAttemptStatus.Unknown, startedAt, startedAt + VerifyWindow), (attempt.Status, attempt.UnknownSince, attempt.ReversalExpectedAt));

        await AssertRejectedAsync(8007, () => _harness.SelectAsync(session.Id, [new(intent.PaymentMethodOptionId, Amount)]));

        _harness.Clock.Advance(VerifyWindow);
        await _harness.SweepAsync();

        var released = await _harness.GetAsync(session.Id);
        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, released.Status);
        Assert.Equal(IntentFailureCode.ProviderReversed, released.FailureCode);
        Assert.Equal(ProviderPaymentAttemptStatus.Reversed, Assert.Single(_harness.AttemptsOf(intent.Id)).Status);
    }

    [Fact]
    public async Task An_unknown_start_with_inquiry_is_resolved_by_reconciliation()
    {
        _harness.ConfigureProfile(MockFundingLedger.PrimaryPgwProfile, profile => profile.Mode = MockPgwMode.UnknownAfterEffect);
        var (session, intent) = await _harness.StartPgwAsync();

        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal(ProviderPaymentAttemptStatus.Unknown, attempt.Status);
        Assert.Null(attempt.ReversalExpectedAt);
        await AssertRejectedAsync(8007, () => _harness.SelectAsync(session.Id, [new(intent.PaymentMethodOptionId, Amount)]));

        var reconciled = await _harness.ReconcileAsync(intent.Id);

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, reconciled.Status);
        Assert.Equal(ProviderPaymentAttemptStatus.Failed, Assert.Single(_harness.AttemptsOf(intent.Id)).Status);
    }

    [Fact]
    public async Task A_route_that_refuses_before_any_effect_falls_through_to_the_next_profile()
    {
        _harness.ConfigureProfile(MockFundingLedger.PrimaryPgwProfile, profile => profile.Mode = MockPgwMode.UnavailableBeforeEffect);

        var (_, intent) = await _harness.StartPgwAsync();

        Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, intent.Status);
        Assert.Equal(
            [(MockFundingLedger.PrimaryPgwProfile, ProviderPaymentAttemptStatus.Failed), (MockFundingLedger.SecondaryPgwProfile, ProviderPaymentAttemptStatus.CustomerActionPending)],
            _harness.AttemptsOf(intent.Id).Select(attempt => (attempt.ProviderProfileId, attempt.Status)));
        Assert.Equal(["UnavailableBeforeEffect", "TokenIssued"], _harness.Ledger.RouteAttempts.Select(route => route.Result));
    }

    [Fact]
    public async Task When_every_route_refuses_the_intent_fails_and_the_session_accepts_another_method()
    {
        _harness.ConfigureProfile(MockFundingLedger.PrimaryPgwProfile, profile => profile.Mode = MockPgwMode.UnavailableBeforeEffect);
        _harness.ConfigureProfile(MockFundingLedger.SecondaryPgwProfile, profile => profile.Mode = MockPgwMode.UnavailableBeforeEffect);

        var (session, intent) = await _harness.StartPgwAsync();

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, session.Status);
        Assert.Equal(IntentFailureCode.ProviderUnavailable, session.FailureCode);
        Assert.Equal(PaymentIntentStatus.Failed, intent.Status);
        Assert.Empty(_harness.Ledger.OperationsOf(intent.Id));

        _harness.ConfigureProfile(MockFundingLedger.SecondaryPgwProfile, profile => profile.Mode = MockPgwMode.Normal);
        var retried = await _harness.SelectAsync(session, TenderType.IranianPgw);

        Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, retried.Intents.Last().Status);
    }

    [Fact]
    public async Task A_customer_who_never_pays_lets_the_intent_expire_and_frees_the_session()
    {
        var (session, intent) = await _harness.StartPgwAsync();

        _harness.Clock.Now = intent.NextAction!.ExpiresAt!.Value.AddSeconds(1);
        await _harness.SweepAsync();

        var swept = await _harness.GetAsync(session.Id);
        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, swept.Status);
        Assert.Equal(PaymentIntentStatus.Expired, swept.Intents.Single().Status);
        Assert.Equal(ProviderPaymentAttemptStatus.Failed, Assert.Single(_harness.AttemptsOf(intent.Id)).Status);

        var retried = await _harness.SelectAsync(swept, TenderType.IranianPgw);
        Assert.Equal(2, retried.Intents.Count);
    }
}
