using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class B2cCheckoutTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task S05_B2c_pgw_redirect_then_server_verify_makes_the_session_paid()
    {
        var session = (await _harness.CreateAsync(assurance: PaymentAssuranceRequirement.FundsReceived)).Session;

        var awaiting = await _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount));
        var leg = Leg(awaiting, TenderType.IranianPgw);

        Assert.Equal(PaymentSessionStatus.RequiresCustomerAction, awaiting.Session.Status);
        Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, leg.Status);
        Assert.Equal(CustomerActionType.Redirect, leg.NextAction!.Type);
        Assert.StartsWith("http://mock.jetpay.test/Mock/v1/Pgw/", leg.NextAction.Url);
        Assert.Equal(0, awaiting.Session.GuaranteedAmount);

        var paid = await _harness.CompleteAsync(leg.Id);

        Assert.Equal(PaymentSessionStatus.Paid, paid.Session.Status);
        Assert.Equal(Amount, paid.Session.CapturedAmount);
        Assert.Equal(Amount, paid.Session.GuaranteedAmount);
        Assert.Equal(0, paid.Session.OutstandingAmount);
        Assert.Equal(PaymentIntentStatus.Captured, Leg(paid, TenderType.IranianPgw).Status);
        Assert.Equal(
            [PaymentSessionStatus.Created, PaymentSessionStatus.Processing, PaymentSessionStatus.RequiresCustomerAction, PaymentSessionStatus.Processing, PaymentSessionStatus.Paid],
            _harness.ChangesOf(session.Id).Select(change => change.Status));
    }

    [Fact]
    public async Task S06_B2c_pgw_decline_leaves_the_session_waiting_for_a_payment_method()
    {
        var session = (await _harness.CreateAsync(assurance: PaymentAssuranceRequirement.FundsReceived)).Session;
        var leg = Leg(await _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount)), TenderType.IranianPgw);

        var declined = await _harness.CompleteAsync(leg.Id, MockCustomerOutcome.Declined);

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, declined.Session.Status);
        Assert.Equal("Declined", declined.Session.FailureCode);
        Assert.Equal(0, declined.Session.GuaranteedAmount);
        Assert.Equal(0, declined.Session.CapturedAmount);
        Assert.Equal(PaymentIntentStatus.Failed, Leg(declined, TenderType.IranianPgw).Status);
    }

    [Fact]
    public async Task S07_Bnpl_commitment_guarantees_the_session_without_paying_it()
    {
        var session = (await _harness.CreateAsync()).Session;
        var awaiting = await _harness.FundAsync(session, null, (TenderType.Bnpl, Amount));

        var guaranteed = await _harness.CompleteAsync(Leg(awaiting, TenderType.Bnpl).Id);
        var leg = Leg(guaranteed, TenderType.Bnpl);

        Assert.Equal(PaymentSessionStatus.RequiresCustomerAction, awaiting.Session.Status);
        Assert.Equal(PaymentSessionStatus.Guaranteed, guaranteed.Session.Status);
        Assert.Equal(Amount, guaranteed.Session.GuaranteedAmount);
        Assert.Equal(0, guaranteed.Session.CapturedAmount);
        Assert.Equal(_harness.Clock.Now.AddDays(1), guaranteed.Session.EarliestGuaranteeExpiry);
        Assert.Equal((PaymentIntentStatus.Authorized, PaymentCaptureMode.Manual, 0m), (leg.Status, leg.CaptureMode, leg.CapturedAmount));
    }

    [Fact]
    public async Task S08_Bnpl_rejection_leaves_nothing_guaranteed()
    {
        var session = (await _harness.CreateAsync()).Session;
        var leg = Leg(await _harness.FundAsync(session, null, (TenderType.Bnpl, Amount)), TenderType.Bnpl);

        var rejected = await _harness.CompleteAsync(leg.Id, MockCustomerOutcome.Declined);

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, rejected.Session.Status);
        Assert.Equal("Declined", rejected.Session.FailureCode);
        Assert.Equal(0, rejected.Session.GuaranteedAmount);
    }

    [Fact]
    public async Task S09_Stored_value_full_debit_pays_the_session_at_once()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 2_000_000m);
        var session = (await _harness.CreateAsync(assurance: PaymentAssuranceRequirement.FundsReceived)).Session;

        var paid = await _harness.FundAsync(session, null, (TenderType.StoredValue, Amount));

        Assert.Equal(PaymentSessionStatus.Paid, paid.Session.Status);
        Assert.Null(Leg(paid, TenderType.StoredValue).NextAction);
        Assert.Equal(1_000_000m, _harness.Ledger.WalletsOf(PayerType.Customer, CustomerId, Irr).Single().Balance);
    }

    [Fact]
    public async Task S10_Stored_value_insufficient_balance_is_refused_before_any_debit()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 500_000m);
        var session = (await _harness.CreateAsync(assurance: PaymentAssuranceRequirement.FundsReceived)).Session;
        var options = await _harness.OptionsFor(session);

        Assert.Equal(500_000m, options.Single(option => option.TenderType == TenderType.StoredValue).AvailableAmount);
        await AssertRejectedAsync(8035, () => _harness.ConfirmAsync(session.Id, [new PaymentSelection(OptionOf(options, TenderType.StoredValue), Amount)]));

        Assert.Empty(_harness.Intents.Committed);
        Assert.Empty(_harness.OperationsOf(MockOperationKind.WalletDebit));
        Assert.Equal(500_000m, _harness.Ledger.WalletsOf(PayerType.Customer, CustomerId, Irr).Single().Balance);
    }

    [Fact]
    public async Task S11_Partial_stored_value_plus_pgw_remainder_pays_the_session()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 300_000m);
        var session = (await _harness.CreateAsync(assurance: PaymentAssuranceRequirement.FundsReceived)).Session;

        var awaiting = await _harness.FundAsync(session, null, (TenderType.StoredValue, 300_000m), (TenderType.IranianPgw, 700_000m));

        Assert.Equal(PaymentSessionStatus.RequiresCustomerAction, awaiting.Session.Status);
        Assert.Equal(300_000m, awaiting.Session.GuaranteedAmount);
        Assert.Equal(700_000m, awaiting.Session.OutstandingAmount);
        Assert.Equal(PaymentIntentStatus.Captured, Leg(awaiting, TenderType.StoredValue).Status);

        var paid = await _harness.CompleteAsync(Leg(awaiting, TenderType.IranianPgw).Id);

        Assert.Equal(PaymentSessionStatus.Paid, paid.Session.Status);
        Assert.Equal(Amount, paid.Session.CapturedAmount);
        Assert.Equal(0, _harness.Ledger.WalletsOf(PayerType.Customer, CustomerId, Irr).Single().Balance);
    }

    public void Dispose() => _harness.Dispose();
}
