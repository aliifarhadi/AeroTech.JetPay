using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.Messages.JetPay.Enums;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.PaymentIntents;

public sealed class TenderBehaviorTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task IranianPgw_requires_a_redirect_and_is_guaranteed_only_after_verified_capture()
    {
        var awaitingCustomer = await _harness.CreateAndConfirmAsync(TenderType.IranianPgw);

        Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, awaitingCustomer.Status);
        Assert.Equal(CustomerActionType.Redirect, awaitingCustomer.NextAction!.Type);
        Assert.StartsWith("http://mock.jetpay.test/Mock/v1/Customer-Actions/", awaitingCustomer.NextAction.Url);
        Assert.Equal(0, awaitingCustomer.GuaranteedAmount);
        Assert.Equal(0, awaitingCustomer.CapturedAmount);

        var captured = await _harness.CompleteCustomerActionAsync(awaitingCustomer.Id);

        Assert.Equal(awaitingCustomer.Id, captured.Id);
        Assert.Equal(PaymentIntentStatus.Captured, captured.Status);
        Assert.Equal(PaymentHarness.Amount, captured.CapturedAmount);
        Assert.Equal(captured.CapturedAmount, captured.GuaranteedAmount);
        Assert.Null(captured.GuaranteeExpiresAt);
        Assert.Null(captured.NextAction);
        Assert.Equal(TenderType.IranianPgw, captured.SelectedTenderType);
        Assert.Equal(
            [PaymentIntentStatus.Created, PaymentIntentStatus.RequiresCustomerAction, PaymentIntentStatus.Processing, PaymentIntentStatus.Captured],
            _harness.ChangesOf(captured.Id).Select(change => change.Status));
    }

    [Fact]
    public async Task IranianPgw_verified_within_confirm_is_captured_directly()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.IranianPgwCaptured);

        var captured = await _harness.CreateAndConfirmAsync(TenderType.IranianPgw);

        Assert.Equal(PaymentIntentStatus.Captured, captured.Status);
        Assert.Equal(PaymentHarness.Amount, captured.GuaranteedAmount);
    }

    [Fact]
    public async Task IranianPgw_failed_verification_fails_the_intent_without_guarantee()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.IranianPgwFailed);
        var intent = await _harness.CreateAndConfirmAsync(TenderType.IranianPgw);

        var failed = await _harness.CompleteCustomerActionAsync(intent.Id);

        Assert.Equal(PaymentIntentStatus.Failed, failed.Status);
        Assert.Equal("Declined", failed.FailureCode);
        Assert.Equal(0, failed.GuaranteedAmount);
        Assert.Equal(0, failed.CapturedAmount);
    }

    [Fact]
    public async Task StoredValue_with_enough_balance_is_captured_synchronously_without_redirect()
    {
        var captured = await _harness.CreateAndConfirmAsync(TenderType.StoredValue);

        Assert.Equal(PaymentIntentStatus.Captured, captured.Status);
        Assert.Null(captured.NextAction);
        Assert.Equal(PaymentHarness.Amount, captured.CapturedAmount);
        Assert.Equal(PaymentHarness.Amount, captured.GuaranteedAmount);
    }

    [Fact]
    public async Task StoredValue_with_insufficient_balance_fails()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.StoredValueInsufficientBalance);

        var failed = await _harness.CreateAndConfirmAsync(TenderType.StoredValue);

        Assert.Equal(PaymentIntentStatus.Failed, failed.Status);
        Assert.Equal("InsufficientFunds", failed.FailureCode);
        Assert.Null(failed.NextAction);
        Assert.Equal(0, failed.CapturedAmount);
        Assert.Equal(0, failed.GuaranteedAmount);
    }

    [Fact]
    public async Task Bnpl_approval_is_an_issuance_guarantee_without_capture()
    {
        var awaitingCustomer = await _harness.CreateAndConfirmAsync(TenderType.Bnpl);
        Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, awaitingCustomer.Status);

        var authorized = await _harness.CompleteCustomerActionAsync(awaitingCustomer.Id);

        Assert.Equal(PaymentIntentStatus.Authorized, authorized.Status);
        Assert.Equal(PaymentHarness.Amount, authorized.AuthorizedAmount);
        Assert.Equal(PaymentHarness.Amount, authorized.GuaranteedAmount);
        Assert.Equal(0, authorized.CapturedAmount);
        Assert.Equal(_harness.Clock.Now.AddDays(1), authorized.GuaranteeExpiresAt);
    }

    [Fact]
    public async Task Bnpl_without_issuance_guarantee_support_cannot_serve_an_authorization_guarantee()
    {
        using var harness = new PaymentHarness(options => options.Bnpl.SupportsIssuanceGuaranteeOnAuthorization = false);
        var intent = await harness.CreateForAsync(TenderType.Bnpl);

        await PaymentHarness.AssertRejectedAsync(8031, () => harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.Bnpl)));
    }

    [Fact]
    public async Task Bnpl_approval_for_a_paid_guarantee_counts_only_once_captured()
    {
        using var harness = new PaymentHarness(options => options.Bnpl.SupportsIssuanceGuaranteeOnAuthorization = false);
        var intent = await harness.CreateAsync(guarantee: RequiredGuarantee.PaidBeforeIssuance, captureMode: PaymentCaptureMode.Manual);
        await harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.Bnpl));

        var authorized = await harness.CompleteCustomerActionAsync(intent.Id);
        var captured = await harness.CaptureAsync(intent.Id);

        Assert.Equal(PaymentIntentStatus.Authorized, authorized.Status);
        Assert.Equal(0, authorized.GuaranteedAmount);
        Assert.Null(authorized.GuaranteeExpiresAt);
        Assert.Equal(PaymentIntentStatus.Captured, captured.Status);
        Assert.Equal(PaymentHarness.Amount, captured.GuaranteedAmount);
    }

    [Fact]
    public async Task Bnpl_rejection_fails_the_intent()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.BnplRejected);
        var intent = await _harness.CreateAndConfirmAsync(TenderType.Bnpl);

        var failed = await _harness.CompleteCustomerActionAsync(intent.Id);

        Assert.Equal(PaymentIntentStatus.Failed, failed.Status);
        Assert.Equal("Declined", failed.FailureCode);
        Assert.Equal(0, failed.GuaranteedAmount);
    }

    [Fact]
    public async Task AgencyCredit_with_available_limit_is_authorized_immediately_as_guarantee_without_capture()
    {
        var authorized = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);

        Assert.Equal(PaymentIntentStatus.Authorized, authorized.Status);
        Assert.Null(authorized.NextAction);
        Assert.Equal(PaymentHarness.Amount, authorized.AuthorizedAmount);
        Assert.Equal(PaymentHarness.Amount, authorized.GuaranteedAmount);
        Assert.Equal(0, authorized.CapturedAmount);
        Assert.Null(authorized.GuaranteeExpiresAt);
    }

    [Fact]
    public async Task AgencyCredit_with_insufficient_limit_fails()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.AgencyCreditInsufficientLimit);

        var failed = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);

        Assert.Equal(PaymentIntentStatus.Failed, failed.Status);
        Assert.Equal("InsufficientFunds", failed.FailureCode);
        Assert.Equal(0, failed.GuaranteedAmount);
    }

    [Theory]
    [InlineData(TenderType.Bnpl)]
    [InlineData(TenderType.AgencyCredit)]
    public async Task Authorization_only_states_never_report_captured_money(TenderType tender)
    {
        var intent = await _harness.CreateAndConfirmAsync(tender);

        if (intent.Status == PaymentIntentStatus.RequiresCustomerAction)
            intent = await _harness.CompleteCustomerActionAsync(intent.Id);

        Assert.Equal(PaymentIntentStatus.Authorized, intent.Status);
        Assert.Equal(0, (await _harness.GetAsync(intent.Id)).CapturedAmount);
        Assert.All(
            _harness.ChangesOf(intent.Id).Where(change => change.Status == PaymentIntentStatus.Authorized),
            change => Assert.Equal(0, change.CapturedAmount));
    }

    [Fact]
    public async Task Authorized_intent_is_captured_later_and_the_guarantee_becomes_captured_money()
    {
        var authorized = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);

        var captured = await _harness.CaptureAsync(authorized.Id);

        Assert.Equal(PaymentIntentStatus.Captured, captured.Status);
        Assert.Equal(PaymentHarness.Amount, captured.CapturedAmount);
        Assert.Equal(PaymentHarness.Amount, captured.GuaranteedAmount);
        Assert.Null(captured.GuaranteeExpiresAt);
    }

    [Fact]
    public async Task Partial_capture_keeps_the_authorized_guarantee_until_the_final_capture()
    {
        var authorized = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);

        var partial = await _harness.CaptureAsync(authorized.Id, amount: 250_000m, finalCapture: false);
        var final = await _harness.CaptureAsync(authorized.Id);

        Assert.Equal(PaymentIntentStatus.PartiallyCaptured, partial.Status);
        Assert.Equal(250_000m, partial.CapturedAmount);
        Assert.Equal(PaymentHarness.Amount, partial.GuaranteedAmount);
        Assert.Equal(PaymentIntentStatus.Captured, final.Status);
        Assert.Equal(PaymentHarness.Amount, final.CapturedAmount);
    }

    [Fact]
    public async Task Capture_is_rejected_beyond_the_capturable_amount_and_for_automatic_capture()
    {
        var authorized = await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit);
        var automatic = await _harness.CreateAndConfirmAsync(TenderType.StoredValue, orderId: 5002);

        await PaymentHarness.AssertRejectedAsync(8007, () => _harness.CaptureAsync(authorized.Id, amount: PaymentHarness.Amount + 1));
        await PaymentHarness.AssertRejectedAsync(8001, () => _harness.CaptureAsync(automatic.Id));
    }

    [Fact]
    public async Task Processing_is_read_back_as_processing_until_the_provider_resolves_it()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.PaymentProcessing);

        var processing = await _harness.CreateAndConfirmAsync(TenderType.StoredValue);
        var readBack = await _harness.GetAsync(processing.Id);
        var resolved = await _harness.CompleteCustomerActionAsync(processing.Id);

        Assert.Equal(PaymentIntentStatus.Processing, readBack.Status);
        Assert.Equal(0, readBack.GuaranteedAmount);
        Assert.Equal(PaymentIntentStatus.Captured, resolved.Status);
    }

    [Fact]
    public async Task An_option_that_cannot_serve_the_intent_terms_is_rejected()
    {
        var automaticPaid = await _harness.CreateForAsync(TenderType.IranianPgw);

        await PaymentHarness.AssertRejectedAsync(8031, () => _harness.ConfirmAsync(automaticPaid.Id, PaymentHarness.OptionFor(TenderType.Bnpl)));
        await PaymentHarness.AssertRejectedAsync(8030, () => _harness.ConfirmAsync(automaticPaid.Id, PaymentHarness.OptionFor(TenderType.AgencyCredit)));
    }

    [Fact]
    public async Task A_scenario_bound_to_another_tender_is_refused_loudly()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.BnplRejected);
        var intent = await _harness.CreateForAsync(TenderType.StoredValue);

        await PaymentHarness.AssertRejectedAsync(8900, () => _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.StoredValue)));
    }

    [Fact]
    public async Task A_payment_intent_binding_overrides_the_order_binding()
    {
        _harness.Scenarios.BindOrder(5001, MockScenario.StoredValueInsufficientBalance);
        var intent = await _harness.CreateForAsync(TenderType.StoredValue);
        _harness.Scenarios.BindPaymentIntent(intent.Id, MockScenario.StoredValueCaptured);

        var captured = await _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.StoredValue));

        Assert.Equal(PaymentIntentStatus.Captured, captured.Status);
    }

    public void Dispose() => _harness.Dispose();
}
