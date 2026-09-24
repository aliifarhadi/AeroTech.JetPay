using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions;
using AeroTech.Messages.JetPay.Enums;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.PaymentMethodOptions;

public sealed class ResolvePaymentMethodOptionsTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task A_customer_is_offered_the_stage3_consumer_tenders()
    {
        var options = await _harness.ResolveAsync(PayerType.Customer);

        Assert.Equal([TenderType.IranianPgw, TenderType.StoredValue, TenderType.Bnpl], options.Select(option => option.TenderType));

        var iranianPgw = options.Single(option => option.TenderType == TenderType.IranianPgw);
        Assert.Equal(CustomerActionType.Redirect, iranianPgw.CustomerActionType);
        Assert.Equal([RequiredGuarantee.PaidBeforeIssuance], iranianPgw.SupportedGuarantees);
        Assert.Equal([PaymentCaptureMode.Automatic], iranianPgw.SupportedCaptureModes);

        var bnpl = options.Single(option => option.TenderType == TenderType.Bnpl);
        Assert.Contains(RequiredGuarantee.AuthorizedBeforeIssuance, bnpl.SupportedGuarantees);
        Assert.Equal([PaymentCaptureMode.Manual], bnpl.SupportedCaptureModes);
    }

    [Fact]
    public async Task An_agency_is_offered_agency_credit_without_customer_action()
    {
        var options = await _harness.ResolveAsync(PayerType.Agency);

        var agencyCredit = options.Single(option => option.TenderType == TenderType.AgencyCredit);
        Assert.Equal(CustomerActionType.None, agencyCredit.CustomerActionType);
        Assert.Contains(RequiredGuarantee.AuthorizedBeforeIssuance, agencyCredit.SupportedGuarantees);
        Assert.DoesNotContain(options, option => option.TenderType == TenderType.Bnpl);
    }

    [Fact]
    public async Task Options_are_normalized_tender_choices_not_provider_routes()
    {
        var options = await _harness.ResolveAsync(PayerType.Customer);

        Assert.All(options, option => Assert.Equal(option.TenderType.ToString(), option.DisplayCode));
        Assert.Equal(options.Count, options.Select(option => option.Id).Distinct().Count());
    }

    [Fact]
    public async Task Resolution_requires_a_complete_eligibility_query()
        => await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _harness.SendAsync(
            new ResolvePaymentMethodOptionsQuery(5001, "ORD-5001", 1, PayerType.Customer, PaymentHarness.PayerId, 0, PaymentHarness.CurrencyId, "Web")));

    public void Dispose() => _harness.Dispose();
}
