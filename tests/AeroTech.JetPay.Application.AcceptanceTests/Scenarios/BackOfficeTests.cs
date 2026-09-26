using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class BackOfficeTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task S19_Three_way_split_fully_covers_the_order()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 300_000m);
        _harness.Credit(TenderType.AgencyCredit, PayerType.Agency, AgencyId, 400_000m);
        _harness.AcceptCashAt();
        var session = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted)).Session;

        var covered = await _harness.FundAsync(session, null, (TenderType.StoredValue, 300_000m), (TenderType.AgencyCredit, 400_000m), (TenderType.Cash, 300_000m));

        Assert.Equal(PaymentSessionStatus.Guaranteed, covered.Session.Status);
        Assert.Equal(Amount, covered.Session.GuaranteedAmount);
        Assert.Equal(600_000m, covered.Session.CapturedAmount);
        Assert.Equal(0, covered.Session.OutstandingAmount);
        Assert.Equal(
            [(TenderType.StoredValue, PaymentIntentStatus.Captured), (TenderType.AgencyCredit, PaymentIntentStatus.Authorized), (TenderType.Cash, PaymentIntentStatus.Captured)],
            covered.PaymentIntents.Select(leg => (leg.TenderType, leg.Status)));
        Assert.Equal(0, _harness.Ledger.WalletsOf(PayerType.Agency, AgencyId, Irr).Single().Balance);
        Assert.Equal(400_000m, _harness.Ledger.FacilitiesOf(PayerType.Agency, AgencyId, Irr).Single().Reserved);
        Assert.Equal(CashOfficeId, Assert.Single(_harness.OperationsOf(MockOperationKind.CashReceipt)).OfficeId);
    }

    [Fact]
    public async Task S20_A_failed_leg_keeps_the_successful_legs_and_the_session_is_not_issue_ready()
    {
        var (session, partial) = await SplitWithDeclinedPgwAsync();

        Assert.Equal(PaymentSessionStatus.PartiallyCovered, partial.Session.Status);
        Assert.Equal(300_000m, partial.Session.GuaranteedAmount);
        Assert.Equal(700_000m, partial.Session.OutstandingAmount);
        Assert.Equal("Declined", partial.Session.FailureCode);
        Assert.Equal(PaymentIntentStatus.Captured, Leg(partial, TenderType.StoredValue).Status);
        Assert.Equal(PaymentIntentStatus.Failed, Leg(partial, TenderType.IranianPgw).Status);
        Assert.Equal(0, _harness.Ledger.WalletsOf(PayerType.Customer, CustomerId, Irr).Single().Balance);
        Assert.NotEqual(PaymentSessionStatus.Guaranteed, (await _harness.GetAsync(session.Id)).Session.Status);
    }

    [Fact]
    public async Task S21_The_outstanding_amount_is_funded_with_a_replacement_leg()
    {
        var (session, _) = await SplitWithDeclinedPgwAsync();

        await AssertRejectedAsync(8034, () => _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount)));

        var awaiting = await _harness.FundAsync(session, null, (TenderType.IranianPgw, 700_000m));
        var paid = await _harness.CompleteAsync(Leg(awaiting, TenderType.IranianPgw).Id);

        Assert.Equal(PaymentSessionStatus.Paid, paid.Session.Status);
        Assert.Equal(Amount, paid.Session.CapturedAmount);
        Assert.Equal(3, paid.PaymentIntents.Count);
    }

    [Fact]
    public async Task S40_Payer_and_initiator_stay_distinct_in_a_staff_assisted_sale()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 400_000m);
        _harness.AcceptCashAt();
        var session = (await _harness.CreateAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.StaffAssisted, PaymentAssuranceRequirement.FundsReceived, BackOffice())).Session;

        var paid = await _harness.FundAsync(session, null, (TenderType.StoredValue, 400_000m), (TenderType.Cash, 600_000m));

        Assert.Equal(PaymentSessionStatus.Paid, paid.Session.Status);
        Assert.Equal((PayerType.Customer, CustomerId), (paid.Session.PayerType, paid.Session.PayerId));
        Assert.Equal(("AirlineEmployee", EmployeeId, (long?)CashOfficeId), (paid.Session.InitiatorContext.ActorType, paid.Session.InitiatorContext.ActorId, paid.Session.InitiatorContext.OfficeId));
        Assert.Equal(0, _harness.Ledger.WalletsOf(PayerType.Customer, CustomerId, Irr).Single().Balance);
        Assert.Empty(_harness.Ledger.WalletsOf(PayerType.Customer, EmployeeId, Irr));
        Assert.Equal(CashOfficeId, Assert.Single(_harness.OperationsOf(MockOperationKind.CashReceipt)).OfficeId);
    }

    private async Task<(PaymentSessionAggregate.Views.PaymentSessionView Session, PaymentSessionAggregate.Views.PaymentSessionResponse Partial)> SplitWithDeclinedPgwAsync()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 300_000m);
        var session = (await _harness.CreateAsync(assurance: PaymentAssuranceRequirement.FundsReceived)).Session;
        var awaiting = await _harness.FundAsync(session, null, (TenderType.StoredValue, 300_000m), (TenderType.IranianPgw, 700_000m));
        var partial = await _harness.CompleteAsync(Leg(awaiting, TenderType.IranianPgw).Id, MockCustomerOutcome.Declined);

        return (session, partial);
    }

    public void Dispose() => _harness.Dispose();
}
