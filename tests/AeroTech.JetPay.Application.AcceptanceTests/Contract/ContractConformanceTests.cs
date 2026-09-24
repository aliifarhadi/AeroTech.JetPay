using System.Reflection;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions;
using AeroTech.Messages;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.JetPay.IntegrationEvents.V1;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.Contract;

/// <summary>
/// Pins the wire shapes to AeroTech-JetPay-Payment-Orchestrator-Benchmark-and-Contract-v1.0-FINAL §6, §17, §18 and §20
/// and to AeroTech-Ordering-Stage3-Payment-Issue-Contract-v1.0-FINAL §5-§6.
/// </summary>
public sealed class ContractConformanceTests
{
    [Fact]
    public void PaymentIntent_exposes_exactly_the_final_contract_fields()
        => AssertShape(typeof(PaymentIntentView), BindingFlags.Public | BindingFlags.Instance,
        [
            ("Id", typeof(string)),
            ("PayableInstructionId", typeof(string)),
            ("OrderId", typeof(long)),
            ("OrderReference", typeof(string)),
            ("CommercialVersion", typeof(int)),
            ("Purpose", typeof(PaymentPurpose)),
            ("PayerType", typeof(PayerType)),
            ("PayerId", typeof(long)),
            ("RequestedAmount", typeof(decimal)),
            ("CurrencyId", typeof(int)),
            ("RequiredGuarantee", typeof(RequiredGuarantee)),
            ("CaptureMode", typeof(PaymentCaptureMode)),
            ("Status", typeof(PaymentIntentStatus)),
            ("AuthorizedAmount", typeof(decimal)),
            ("GuaranteedAmount", typeof(decimal)),
            ("CapturedAmount", typeof(decimal)),
            ("RefundedAmount", typeof(decimal)),
            ("GuaranteeExpiresAt", typeof(DateTimeOffset?)),
            ("IntentExpiresAt", typeof(DateTimeOffset?)),
            ("SelectedTenderType", typeof(TenderType?)),
            ("SelectedPaymentMethodOptionId", typeof(string)),
            ("NextAction", typeof(CustomerActionView)),
            ("FailureCode", typeof(string)),
            ("FailureReason", typeof(string)),
            ("Version", typeof(long)),
            ("CreatedAt", typeof(DateTimeOffset)),
            ("UpdatedAt", typeof(DateTimeOffset))
        ]);

    [Fact]
    public void CustomerAction_exposes_exactly_the_final_contract_fields()
        => AssertShape(typeof(CustomerActionView), BindingFlags.Public | BindingFlags.Instance,
        [
            ("Type", typeof(CustomerActionType)),
            ("Url", typeof(string)),
            ("HttpMethod", typeof(string)),
            ("FormFields", typeof(IReadOnlyDictionary<string, string>)),
            ("ExpiresAt", typeof(DateTimeOffset?))
        ]);

    [Fact]
    public void PaymentMethodOption_exposes_no_provider_code()
        => AssertShape(typeof(PaymentMethodOptionView), BindingFlags.Public | BindingFlags.Instance,
        [
            ("Id", typeof(string)),
            ("TenderType", typeof(TenderType)),
            ("DisplayCode", typeof(string)),
            ("CustomerActionType", typeof(CustomerActionType)),
            ("SupportedGuarantees", typeof(IReadOnlyList<RequiredGuarantee>)),
            ("SupportedCaptureModes", typeof(IReadOnlyList<PaymentCaptureMode>))
        ]);

    [Fact]
    public void PaymentIntentChangedV1_carries_exactly_the_final_contract_fields()
    {
        Assert.Equal(typeof(string), typeof(BaseIntegrationEvent).GetProperty(nameof(BaseIntegrationEvent.EventId))!.PropertyType);
        Assert.True(typeof(PaymentIntentChangedV1).IsSubclassOf(typeof(BaseIntegrationEvent)));

        AssertShape(typeof(PaymentIntentChangedV1), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
        [
            ("PaymentIntentId", typeof(string)),
            ("PayableInstructionId", typeof(string)),
            ("OrderId", typeof(long)),
            ("CommercialVersion", typeof(int)),
            ("Purpose", typeof(PaymentPurpose)),
            ("Status", typeof(PaymentIntentStatus)),
            ("RequiredGuarantee", typeof(RequiredGuarantee)),
            ("CaptureMode", typeof(PaymentCaptureMode)),
            ("RequestedAmount", typeof(decimal)),
            ("AuthorizedAmount", typeof(decimal)),
            ("GuaranteedAmount", typeof(decimal)),
            ("CapturedAmount", typeof(decimal)),
            ("CurrencyId", typeof(int)),
            ("GuaranteeExpiresAt", typeof(DateTimeOffset?)),
            ("IntentExpiresAt", typeof(DateTimeOffset?)),
            ("Version", typeof(long)),
            ("FailureCode", typeof(string)),
            ("OccurredAt", typeof(DateTimeOffset))
        ]);
    }

    [Fact]
    public void PaymentPaidUnappliedV1_carries_exactly_the_final_contract_fields()
    {
        Assert.True(typeof(PaymentPaidUnappliedV1).IsSubclassOf(typeof(BaseIntegrationEvent)));

        AssertShape(typeof(PaymentPaidUnappliedV1), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
        [
            ("PaymentIntentId", typeof(string)),
            ("OrderId", typeof(long)),
            ("SupersededPayableInstructionId", typeof(string)),
            ("CapturedAmount", typeof(decimal)),
            ("CurrencyId", typeof(int)),
            ("ReasonCode", typeof(string)),
            ("OccurredAt", typeof(DateTimeOffset))
        ]);
    }

    [Fact]
    public void Enumerations_match_the_final_contract()
    {
        Assert.Equal(
            ["IranianPgw", "ExternalCard", "Bnpl", "StoredValue", "AgencyDeposit", "CustomerCredit", "AgencyCredit", "CorporateCredit", "BankTransferReference"],
            Enum.GetNames<TenderType>());
        Assert.Equal(["PaidBeforeIssuance", "AuthorizedBeforeIssuance"], Enum.GetNames<RequiredGuarantee>());
        Assert.Equal(
            ["Created", "RequiresCustomerAction", "Processing", "Authorized", "PartiallyCaptured", "Captured", "Failed", "Cancelled", "Expired"],
            Enum.GetNames<PaymentIntentStatus>());
        Assert.Equal(["None", "Redirect", "HtmlForm", "Sdk", "ThreeDsChallenge"], Enum.GetNames<CustomerActionType>());
        Assert.Equal(["Automatic", "Manual"], Enum.GetNames<PaymentCaptureMode>());

        // Ordering Stage-3 Payment Issue Contract §5 (PaymentCoveragePurpose).
        Assert.Equal(
            ["InitialSale", "AddService", "ExchangeAdditionalCollection", "GroupDeposit", "FinalPayment", "Other"],
            Enum.GetNames<PaymentPurpose>());
    }

    private static void AssertShape(Type type, BindingFlags flags, IReadOnlyList<(string Name, Type Type)> expected)
    {
        var actual = type.GetProperties(flags)
            .Select(property => (property.Name, Type: property.PropertyType))
            .ToList();

        Assert.Equal(expected.Select(field => field.Name), actual.Select(field => field.Name));
        Assert.Equal(expected.Select(field => field.Type), actual.Select(field => field.Type));
    }
}
