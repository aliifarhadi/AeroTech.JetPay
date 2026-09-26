using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class PgwRoutingTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task S29_Primary_unavailable_before_any_effect_reroutes_to_the_secondary_route()
    {
        _harness.Ledger.ConfigurePgwProfile(MockFundingLedger.PrimaryPgwRoute, profile => profile.Mode = MockPgwMode.UnavailableBeforeEffect);
        var session = (await _harness.CreateAsync(assurance: PaymentAssuranceRequirement.FundsReceived)).Session;

        var awaiting = await _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount));
        var leg = Leg(awaiting, TenderType.IranianPgw);

        Assert.Contains($"/Pgw/{MockFundingLedger.SecondaryPgwRoute}/", leg.NextAction!.Url);
        Assert.Equal(
            [(MockFundingLedger.PrimaryPgwRoute, "UnavailableBeforeEffect"), (MockFundingLedger.SecondaryPgwRoute, "TokenIssued")],
            _harness.Ledger.RouteAttempts.Where(attempt => attempt.PaymentIntentId == leg.Id).Select(attempt => (attempt.RouteCode, attempt.Result)));
        Assert.Equal(MockFundingLedger.SecondaryPgwRoute, Assert.Single(_harness.OperationsOf(MockOperationKind.PgwTransaction)).RouteCode);
        Assert.Equal(PaymentSessionStatus.Paid, (await _harness.CompleteAsync(leg.Id)).Session.Status);
    }

    [Fact]
    public async Task S30_An_unknown_effect_is_never_rerouted_before_reconciliation()
    {
        _harness.Ledger.ConfigurePgwProfile(MockFundingLedger.PrimaryPgwRoute, profile => profile.Mode = MockPgwMode.UnknownAfterEffect);
        var session = (await _harness.CreateAsync(assurance: PaymentAssuranceRequirement.FundsReceived)).Session;

        var pending = await _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount));
        var leg = Leg(pending, TenderType.IranianPgw);

        Assert.Equal(PaymentSessionStatus.Processing, pending.Session.Status);
        Assert.Equal(PaymentIntentStatus.Processing, leg.Status);
        Assert.Null(leg.NextAction);
        await AssertRejectedAsync(8005, () => _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount)));
        Assert.Single(_harness.Ledger.RouteAttempts);
        Assert.Single(_harness.OperationsOf(MockOperationKind.PgwTransaction));

        var reconciled = await _harness.ReconcileAsync(leg.Id);

        Assert.Equal(PaymentSessionStatus.Paid, reconciled.Session.Status);
        Assert.Single(_harness.OperationsOf(MockOperationKind.PgwTransaction));
    }

    [Fact]
    public async Task S30_Reconciliation_that_proves_no_effect_frees_the_amount_for_a_new_attempt()
    {
        _harness.Ledger.ConfigurePgwProfile(MockFundingLedger.PrimaryPgwRoute, profile =>
        {
            profile.Mode = MockPgwMode.UnknownAfterEffect;
            profile.InquiryOutcome = MockInquiryOutcome.NoEffect;
        });
        var session = (await _harness.CreateAsync(assurance: PaymentAssuranceRequirement.FundsReceived)).Session;
        var leg = Leg(await _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount)), TenderType.IranianPgw);

        var reconciled = await _harness.ReconcileAsync(leg.Id);
        _harness.Ledger.ConfigurePgwProfile(MockFundingLedger.PrimaryPgwRoute, profile => profile.Mode = MockPgwMode.Normal);
        var retried = await _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount));

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, reconciled.Session.Status);
        Assert.Equal(("NoProviderEffect", PaymentIntentStatus.Failed), (Leg(reconciled, TenderType.IranianPgw).FailureCode, Leg(reconciled, TenderType.IranianPgw).Status));
        Assert.Equal(PaymentSessionStatus.RequiresCustomerAction, retried.Session.Status);
        Assert.Equal(2, retried.PaymentIntents.Count);
    }

    public void Dispose() => _harness.Dispose();
}
