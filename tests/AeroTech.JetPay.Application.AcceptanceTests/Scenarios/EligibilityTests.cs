using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class EligibilityTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A01_B2C_resolves_the_pgw_and_the_customers_own_wallet_for_its_currency()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 250_000m);
        _harness.Wallet(PayerType.Customer, CustomerId + 1, 9_000_000m);

        var options = await _harness.ResolveAsync();

        Assert.Equal([TenderType.IranianPgw, TenderType.StoredValue], options.Select(option => option.TenderType).OrderBy(tender => tender));

        var pgw = options.Single(option => option.TenderType == TenderType.IranianPgw);
        Assert.Equal(CustomerActionType.Redirect, pgw.CustomerActionType);
        Assert.False(pgw.CanAutoSelect);
        Assert.False(pgw.SupportsPartialAmount);
        Assert.Null(pgw.AvailableAmount);
        Assert.Equal([PaymentAssuranceRequirement.FundsReceived, PaymentAssuranceRequirement.IssuanceGuaranteed], pgw.SupportedAssuranceRequirements);

        var wallet = options.Single(option => option.TenderType == TenderType.StoredValue);
        Assert.Equal(250_000m, wallet.AvailableAmount);
        Assert.Equal(CustomerActionType.None, wallet.CustomerActionType);
        Assert.False(wallet.CanAutoSelect);
    }

    [Fact]
    public async Task A01_Unattended_agency_sees_only_its_wallets_and_the_default_can_be_auto_selected()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 5_000_000m, isDefault: true);
        _harness.Wallet(PayerType.Agency, AgencyId, 1_000_000m, code: "spare");

        var options = await _harness.ResolveAsync(PayerType.Agency, AgencyId, PaymentInteractionMode.UnattendedApi);

        Assert.All(options, option => Assert.Equal(TenderType.StoredValue, option.TenderType));
        Assert.Equal([true, false], options.Select(option => option.CanAutoSelect));
        Assert.True(options[0].IsDefault);
    }

    [Fact]
    public async Task Staff_assisted_payment_does_not_offer_a_customer_redirect_in_stage_a()
    {
        var options = await _harness.ResolveAsync(interactionMode: PaymentInteractionMode.StaffAssisted);

        Assert.DoesNotContain(options, option => option.TenderType == TenderType.IranianPgw);
    }

    [Fact]
    public async Task The_pgw_is_offered_only_when_an_active_profile_accepts_the_amount()
    {
        _harness.ConfigureProfile(MockFundingLedger.PrimaryPgwProfile, profile => profile.MaximumAmount = 500_000m);
        _harness.ConfigureProfile(MockFundingLedger.SecondaryPgwProfile, profile => profile.MaximumAmount = 800_000m);

        Assert.DoesNotContain(await _harness.ResolveAsync(amount: 900_000m), option => option.TenderType == TenderType.IranianPgw);

        var pgw = (await _harness.ResolveAsync(amount: 700_000m)).Single(option => option.TenderType == TenderType.IranianPgw);
        Assert.Equal(800_000m, pgw.MaximumAmount);

        _harness.ConfigureProfile(MockFundingLedger.SecondaryPgwProfile, profile => profile.Enabled = false);
        Assert.DoesNotContain(await _harness.ResolveAsync(amount: 700_000m), option => option.TenderType == TenderType.IranianPgw);
    }

    [Fact]
    public async Task A14_A_wallet_in_another_currency_is_not_eligible()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 9_000_000m, currencyId: Usd);
        _harness.Wallet(PayerType.Agency, AgencyId, 9_000_000m, isDefault: true, currencyId: Usd);

        Assert.DoesNotContain(await _harness.ResolveAsync(), option => option.TenderType == TenderType.StoredValue);

        var agency = await _harness.CreateAgencyDefaultAsync();

        Assert.Equal(PaymentSessionStatus.RequiresPaymentMethod, agency.Status);
        Assert.Equal(FundingFailureCode.DefaultFundingSourceUnavailable, agency.FailureCode);
        Assert.Empty(agency.Intents);
    }

    [Fact]
    public async Task Option_ids_are_stable_for_the_same_payer_tender_and_currency()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, 250_000m);

        var first = await _harness.ResolveAsync();
        var second = await _harness.ResolveAsync(amount: 10m);

        Assert.Equal(first.Select(option => option.Id).Order(), second.Select(option => option.Id).Order());
    }
}
