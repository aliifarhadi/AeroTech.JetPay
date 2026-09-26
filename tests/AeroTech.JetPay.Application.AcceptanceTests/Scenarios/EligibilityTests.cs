using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class EligibilityTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task S01_B2c_web_sees_eligible_payment_methods_for_amount_and_currency()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 500_000m);

        var options = await _harness.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.CustomerInteractive);

        Assert.Equal([TenderType.StoredValue, TenderType.IranianPgw, TenderType.Bnpl], options.Select(option => option.TenderType));
        Assert.All(options, option => Assert.Equal(Irr, option.CurrencyId));
        Assert.Equal(CustomerActionType.Redirect, options.Single(option => option.TenderType == TenderType.IranianPgw).CustomerActionType);
        Assert.Equal(CustomerActionType.None, options.Single(option => option.TenderType == TenderType.StoredValue).CustomerActionType);
    }

    [Fact]
    public async Task S02_Ineligible_methods_do_not_appear()
    {
        _harness.Credit(TenderType.CustomerCredit, PayerType.Customer, CustomerId, 5_000_000m);
        _harness.AcceptCashAt();
        _harness.Ledger.SetBnpl(enabled: true, null, null, [(PayerType.Customer, CustomerId)]);

        var web = await _harness.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.CustomerInteractive, initiator: BackOffice() with { SalesChannel = Messages.Shared.Enums.SalesChannel.IBE });
        var fundsReceived = await _harness.ResolveAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted, PaymentAssuranceRequirement.FundsReceived);

        Assert.DoesNotContain(web, option => option.TenderType is TenderType.CustomerCredit or TenderType.Cash or TenderType.Bnpl);
        Assert.DoesNotContain(fundsReceived, option => option.AssuranceCapability == PaymentAssuranceCapability.CommitmentToPay);
    }

    [Fact]
    public async Task S03_StoredValue_option_reports_its_currency_specific_balance()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 500_000m, currencyId: Irr);
        _harness.Wallet(PayerType.Customer, CustomerId, 200m, currencyId: Usd);

        var irr = (await _harness.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.CustomerInteractive, currencyId: Irr))
            .Single(option => option.TenderType == TenderType.StoredValue);
        var usd = (await _harness.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.CustomerInteractive, amount: 150m, currencyId: Usd))
            .Single(option => option.TenderType == TenderType.StoredValue);

        Assert.Equal((Irr, 500_000m), (irr.CurrencyId, irr.AvailableAmount!.Value));
        Assert.Equal((Usd, 200m), (usd.CurrencyId, usd.AvailableAmount!.Value));
        Assert.True(irr.SupportsPartialAmount);
        Assert.NotEqual(irr.Id, usd.Id);
    }

    [Fact]
    public async Task S04_Default_wallet_is_marked_only_for_its_payer_and_currency()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 5_000_000m, isDefault: true, code: "main");
        _harness.Wallet(PayerType.Agency, AgencyId, 2_000_000m, code: "promo");
        _harness.Wallet(PayerType.Agency, AgencyId, 1_000m, isDefault: true, currencyId: Usd);
        _harness.Wallet(PayerType.Agency, AgencyId + 1, 5_000_000m);

        var agencyIrr = await _harness.ResolveAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.UnattendedApi);
        var otherAgency = await _harness.ResolveAsync(PayerType.Agency, AgencyId + 1, PaymentInteractionMode.UnattendedApi);
        var agencyStaff = await _harness.ResolveAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted);

        var @default = Assert.Single(agencyIrr, option => option.IsDefault);
        Assert.Equal(5_000_000m, @default.AvailableAmount);
        Assert.True(@default.CanAutoSelect);
        Assert.Equal(@default.Id, agencyIrr[0].Id);
        Assert.Contains(agencyIrr, option => option is { TenderType: TenderType.StoredValue, IsDefault: false, AvailableAmount: 2_000_000m });
        Assert.DoesNotContain(otherAgency, option => option.IsDefault);
        Assert.DoesNotContain(agencyStaff, option => option.CanAutoSelect);
    }

    [Fact]
    public async Task S16_Corporate_and_customer_credit_are_eligible_like_agency_credit()
    {
        _harness.Credit(TenderType.CorporateCredit, PayerType.Corporate, CorporateId, 3_000_000m);
        _harness.Credit(TenderType.CustomerCredit, PayerType.Customer, CustomerId, 2_000_000m);

        var corporate = await _harness.ResolveAsync(PayerType.Corporate, CorporateId, PaymentInteractionMode.StaffAssisted);
        var customer = await _harness.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.StaffAssisted);

        var corporateCredit = corporate.Single(option => option.TenderType == TenderType.CorporateCredit);
        Assert.Equal((PaymentAssuranceCapability.CommitmentToPay, PaymentCaptureMode.Manual, 3_000_000m), (corporateCredit.AssuranceCapability, corporateCredit.CaptureMode, corporateCredit.AvailableAmount!.Value));
        Assert.Equal(2_000_000m, customer.Single(option => option.TenderType == TenderType.CustomerCredit).AvailableAmount);

        var session = (await _harness.CreateAsync(PayerType.Corporate, CorporateId, PaymentInteractionMode.StaffAssisted)).Session;
        var guaranteed = await _harness.FundAsync(session, null, (TenderType.CorporateCredit, Amount));

        Assert.Equal(PaymentSessionStatus.Guaranteed, guaranteed.Session.Status);
        Assert.Equal(0, guaranteed.Session.CapturedAmount);
    }

    [Fact]
    public async Task S17_Back_office_customer_sees_stored_value_credit_and_cash_according_to_policy()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 300_000m);
        _harness.Credit(TenderType.CustomerCredit, PayerType.Customer, CustomerId, 2_000_000m);
        _harness.AcceptCashAt();

        var options = await _harness.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.StaffAssisted);

        Assert.Equal([TenderType.StoredValue, TenderType.CustomerCredit, TenderType.Cash], options.Select(option => option.TenderType));

        using var pgwPolicy = new PaymentHarness(allowPgwForStaffAssisted: true);
        Assert.Contains(await pgwPolicy.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.StaffAssisted), option => option.TenderType == TenderType.IranianPgw);
    }

    [Fact]
    public async Task S18_Back_office_agency_sees_stored_value_agency_credit_and_cash()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 300_000m, isDefault: true);
        _harness.Credit(TenderType.AgencyCredit, PayerType.Agency, AgencyId, 4_000_000m);
        _harness.AcceptCashAt();

        var options = await _harness.ResolveAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted);

        Assert.Equal([TenderType.StoredValue, TenderType.AgencyCredit, TenderType.Cash], options.Select(option => option.TenderType));
    }

    [Fact]
    public async Task S22_Cash_is_unavailable_outside_staff_assisted_sales()
    {
        _harness.AcceptCashAt();

        var web = await _harness.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.CustomerInteractive, initiator: Web with { OfficeId = CashOfficeId });
        var agencyApi = await _harness.ResolveAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.UnattendedApi, initiator: AgencyApi with { OfficeId = CashOfficeId });
        var otherOffice = await _harness.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.StaffAssisted, initiator: BackOffice(999));
        var staff = await _harness.ResolveAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.StaffAssisted);

        Assert.DoesNotContain(web, option => option.TenderType == TenderType.Cash);
        Assert.DoesNotContain(agencyApi, option => option.TenderType == TenderType.Cash);
        Assert.DoesNotContain(otherOffice, option => option.TenderType == TenderType.Cash);

        var unattended = (await _harness.CreateAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.UnattendedApi)).Session;
        await AssertRejectedAsync(8030, () => _harness.ConfirmAsync(unattended.Id, [new PaymentSelection(OptionOf(staff, TenderType.Cash), Amount)]));
    }

    [Fact]
    public async Task S23_A_wallet_in_another_currency_is_not_eligible()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 5_000m, currencyId: Usd);

        var irr = await _harness.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.CustomerInteractive);
        var usd = await _harness.ResolveAsync(PayerType.Customer, CustomerId, PaymentInteractionMode.CustomerInteractive, amount: 100m, currencyId: Usd);

        Assert.DoesNotContain(irr, option => option.TenderType == TenderType.StoredValue);

        var session = (await _harness.CreateAsync()).Session;
        await AssertRejectedAsync(8030, () => _harness.ConfirmAsync(session.Id, [new PaymentSelection(OptionOf(usd, TenderType.StoredValue), Amount)]));
        Assert.Empty(_harness.Intents.Committed);
    }

    public void Dispose() => _harness.Dispose();
}
