using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class B2cPgwTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A02_Selecting_the_pgw_returns_a_redirect_backed_by_a_provider_attempt()
    {
        var started = _harness.Clock.Now;
        var (session, intent) = await _harness.StartPgwAsync();

        Assert.Equal(PaymentSessionStatus.Processing, session.Status);
        Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, intent.Status);
        Assert.Equal(CustomerActionType.Redirect, intent.NextAction!.Type);
        Assert.Equal($"http://mock.jetpay.test/Mock/v1/Pgw/{MockFundingLedger.PrimaryPgwProfile}/Pay/{intent.Id}", intent.NextAction.Url);
        Assert.Equal(started + CustomerActionTtl, intent.NextAction.ExpiresAt);

        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal((1, MockFundingLedger.PrimaryPgwProfile, ProviderPaymentAttemptStatus.CustomerActionPending), (attempt.AttemptNumber, attempt.ProviderProfileId, attempt.Status));
        Assert.Equal($"jetpay-attempt-{attempt.Id}", attempt.IdempotencyKey);
        Assert.NotNull(attempt.ProviderTransactionRef);
    }

    [Fact]
    public async Task A03_A_callback_is_evidence_on_the_attempt_and_never_makes_the_session_paid_by_itself()
    {
        var (_, intent) = await _harness.StartPgwAsync();

        var afterForgedCallback = await _harness.CallbackAsync(intent.Id);

        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal(_harness.Clock.Now, attempt.CallbackReceivedAt);
        Assert.Equal(_harness.Clock.Now + VerifyWindow, attempt.VerifyDeadline);
        Assert.Equal(ProviderPaymentAttemptStatus.Failed, attempt.Status);
        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, afterForgedCallback.Status);
        Assert.Equal(0m, afterForgedCallback.CapturedAmount);
        Assert.Equal(PaymentIntentStatus.Failed, afterForgedCallback.Intents.Single().Status);
    }

    [Fact]
    public async Task A04_Verify_then_the_required_settle_make_the_session_paid()
    {
        var (_, intent) = await _harness.StartPgwAsync();
        _harness.Clock.Advance(TimeSpan.FromMinutes(2));

        var paid = await _harness.CompleteAsync(intent.Id);

        Assert.Equal(PaymentSessionStatus.Paid, paid.Status);
        Assert.Equal((Amount, Amount, 0m), (paid.CapturedAmount, paid.GuaranteedAmount, paid.OutstandingAmount));
        Assert.Equal(PaymentIntentStatus.Captured, paid.Intents.Single().Status);

        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal(ProviderPaymentAttemptStatus.Settled, attempt.Status);
        Assert.NotNull(attempt.VerificationStartedAt);
        Assert.NotNull(attempt.VerifiedAt);
        Assert.NotNull(attempt.SettledAt);

        var transaction = _harness.Ledger.OperationsOf(intent.Id).Single();
        Assert.NotNull(transaction.VerifiedAt);
        Assert.NotNull(transaction.SettledAt);
    }

    [Fact]
    public async Task A04_A_provider_that_needs_no_settlement_is_paid_on_verify()
    {
        _harness.ConfigureProfile(MockFundingLedger.PrimaryPgwProfile, profile => profile.Enabled = false);
        var (_, intent) = await _harness.StartPgwAsync();

        var paid = await _harness.CompleteAsync(intent.Id);

        Assert.Equal(PaymentSessionStatus.Paid, paid.Status);
        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal((MockFundingLedger.SecondaryPgwProfile, ProviderPaymentAttemptStatus.Verified), (attempt.ProviderProfileId, attempt.Status));
        Assert.Null(_harness.Ledger.OperationsOf(intent.Id).Single().SettledAt);
    }

    [Fact]
    public async Task A05_A_callback_after_the_verify_deadline_is_never_captured()
    {
        var (_, intent) = await _harness.StartPgwAsync();
        var paidAt = _harness.Clock.Now;
        _harness.PayAtGateway(intent.Id);
        _harness.Clock.Advance(VerifyWindow + TimeSpan.FromMinutes(1));

        var late = await _harness.CallbackAsync(intent.Id);

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, late.Status);
        Assert.Equal(0m, late.CapturedAmount);
        Assert.Equal((PaymentIntentStatus.Failed, IntentFailureCode.VerifyDeadlineElapsed), (late.Intents.Single().Status, late.Intents.Single().FailureCode));

        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal(ProviderPaymentAttemptStatus.AutoReversalPending, attempt.Status);
        Assert.Equal(paidAt + VerifyWindow, attempt.ReversalExpectedAt);
        Assert.Null(attempt.VerificationStartedAt);
        Assert.Null(_harness.Ledger.OperationsOf(intent.Id).Single().VerifiedAt);
        Assert.Empty(_harness.PaidUnapplied());

        await _harness.SweepAsync();

        Assert.Equal(ProviderPaymentAttemptStatus.Reversed, Assert.Single(_harness.AttemptsOf(intent.Id)).Status);
    }

    [Fact]
    public async Task A06_A_late_unverified_callback_on_an_expired_session_takes_the_auto_reversal_path()
    {
        var (session, intent) = await _harness.StartPgwAsync(expiresAt: _harness.Clock.Now.AddMinutes(5));
        _harness.Clock.Advance(TimeSpan.FromMinutes(4));
        _harness.PayAtGateway(intent.Id);
        _harness.Clock.Advance(TimeSpan.FromMinutes(2));

        await _harness.CallbackAsync(intent.Id);
        await _harness.SweepAsync();

        var expired = await _harness.GetAsync(session.Id);
        Assert.Equal(PaymentSessionStatus.Expired, expired.Status);
        Assert.Equal(0m, expired.CapturedAmount);

        var attempt = Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Equal(ProviderPaymentAttemptStatus.AutoReversalPending, attempt.Status);
        Assert.Null(attempt.VerificationStartedAt);
        Assert.Null(_harness.Ledger.OperationsOf(intent.Id).Single().VerifiedAt);
        Assert.Empty(_harness.PaidUnapplied());
    }

    [Fact]
    public async Task A06_A_callback_arriving_after_the_expiry_sweep_is_not_verified_either()
    {
        _harness.ConfigureProfile(MockFundingLedger.PrimaryPgwProfile, profile => profile.Enabled = false);
        var (session, intent) = await _harness.StartPgwAsync(expiresAt: _harness.Clock.Now.AddMinutes(5));
        _harness.Clock.Advance(TimeSpan.FromMinutes(4));
        _harness.PayAtGateway(intent.Id);
        _harness.Clock.Advance(TimeSpan.FromMinutes(2));
        await _harness.SweepAsync();

        var afterCallback = await _harness.CallbackAsync(intent.Id);

        Assert.Equal(PaymentSessionStatus.Expired, afterCallback.Status);
        Assert.Equal(PaymentIntentStatus.Expired, afterCallback.Intents.Single().Status);
        Assert.Equal(ProviderPaymentAttemptStatus.AutoReversalPending, Assert.Single(_harness.AttemptsOf(intent.Id)).Status);
        Assert.Null(_harness.Ledger.OperationsOf(intent.Id).Single().VerifiedAt);
        Assert.Empty(_harness.PaidUnapplied());
        Assert.Equal(session.Id, afterCallback.Id);
    }

    [Fact]
    public async Task A07_A_callback_for_money_verified_before_the_payable_was_superseded_reports_paid_unapplied_once()
    {
        var (session, intent) = await _harness.StartPgwAsync();
        _harness.PayAtGateway(intent.Id);
        Assert.Equal(PaymentSessionStatus.Paid, (await _harness.ReconcileAsync(intent.Id)).Status);

        await _harness.CreateAsync(commercialVersion: 2);

        await _harness.CallbackAsync(intent.Id);
        await _harness.CallbackAsync(intent.Id);

        var unapplied = Assert.Single(_harness.PaidUnapplied());
        Assert.Equal(session.Id, unapplied.PaymentSessionId);
        Assert.Equal(intent.Id, unapplied.PaymentIntentId);
        Assert.Equal(session.PayableInstructionId, unapplied.SupersededPayableInstructionId);
        Assert.Equal((Amount, Irr, PaidUnappliedReason.PayableInstructionSuperseded), (unapplied.CapturedAmount, unapplied.CurrencyId, unapplied.ReasonCode));
    }

    [Fact]
    public async Task A07_An_unverified_callback_on_a_superseded_payable_is_not_verified_and_reports_nothing()
    {
        var (_, intent) = await _harness.StartPgwAsync();
        _harness.PayAtGateway(intent.Id);
        await _harness.CreateAsync(commercialVersion: 2);

        var afterCallback = await _harness.CallbackAsync(intent.Id);

        Assert.Equal(0m, afterCallback.CapturedAmount);
        Assert.Equal(ProviderPaymentAttemptStatus.AutoReversalPending, Assert.Single(_harness.AttemptsOf(intent.Id)).Status);
        Assert.Empty(_harness.PaidUnapplied());
    }

    [Fact]
    public async Task A_declined_payment_fails_the_intent_and_the_customer_can_try_again()
    {
        var (session, intent) = await _harness.StartPgwAsync();

        var declined = await _harness.CompleteAsync(intent.Id, MockCustomerOutcome.Declined);

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, declined.Status);
        Assert.Equal("Declined", declined.FailureCode);

        var retried = await _harness.SelectAsync(declined, TenderType.IranianPgw);

        Assert.Equal(2, retried.Intents.Count);
        Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, retried.Intents.Last().Status);
        Assert.Null(retried.FailureCode);
        Assert.Equal(session.Id, retried.Id);
    }
}
