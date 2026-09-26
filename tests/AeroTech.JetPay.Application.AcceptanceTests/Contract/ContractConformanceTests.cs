using System.Reflection;
using AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.Messages;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.JetPay.IntegrationEvents.V1;
using AeroTech.Messages.Shared.Enums;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.Contract;

public sealed class ContractConformanceTests
{
    [Fact]
    public void PaymentSession_exposes_the_master_fields_plus_failure_code_and_outstanding_amount()
        => AssertShape(typeof(PaymentSessionView), BindingFlags.Public | BindingFlags.Instance,
        [
            ("Id", typeof(string)),
            ("PayableInstructionId", typeof(string)),
            ("OrderId", typeof(long)),
            ("OrderReference", typeof(string)),
            ("CommercialVersion", typeof(int)),
            ("Purpose", typeof(PaymentPurpose)),
            ("PayerType", typeof(PayerType)),
            ("PayerId", typeof(long)),
            ("InitiatorContext", typeof(PaymentInitiatorContextView)),
            ("RequiredAmount", typeof(decimal)),
            ("CurrencyId", typeof(int)),
            ("AssuranceRequirement", typeof(PaymentAssuranceRequirement)),
            ("InteractionMode", typeof(PaymentInteractionMode)),
            ("Status", typeof(PaymentSessionStatus)),
            ("GuaranteedAmount", typeof(decimal)),
            ("CapturedAmount", typeof(decimal)),
            ("RefundedAmount", typeof(decimal)),
            ("OutstandingAmount", typeof(decimal)),
            ("EarliestGuaranteeExpiry", typeof(DateTimeOffset?)),
            ("ExpiresAt", typeof(DateTimeOffset?)),
            ("FailureCode", typeof(string)),
            ("Version", typeof(long)),
            ("CreatedAt", typeof(DateTimeOffset)),
            ("UpdatedAt", typeof(DateTimeOffset)),
            ("PaymentIntentIds", typeof(IReadOnlyList<string>))
        ]);

    [Fact]
    public void PaymentInitiatorContext_matches_the_master_contract()
        => AssertShape(typeof(PaymentInitiatorContextView), BindingFlags.Public | BindingFlags.Instance,
        [
            ("SalesChannel", typeof(SalesChannel)),
            ("ActorType", typeof(string)),
            ("ActorId", typeof(long)),
            ("OfficeId", typeof(long?))
        ]);

    [Fact]
    public void PaymentIntent_exposes_the_master_fields_without_internal_provider_evidence()
        => AssertShape(typeof(PaymentIntentView), BindingFlags.Public | BindingFlags.Instance,
        [
            ("Id", typeof(string)),
            ("PaymentSessionId", typeof(string)),
            ("Sequence", typeof(int)),
            ("PaymentMethodOptionId", typeof(string)),
            ("TenderType", typeof(TenderType)),
            ("RequestedAmount", typeof(decimal)),
            ("CurrencyId", typeof(int)),
            ("CaptureMode", typeof(PaymentCaptureMode)),
            ("Status", typeof(PaymentIntentStatus)),
            ("AuthorizedAmount", typeof(decimal)),
            ("GuaranteedAmount", typeof(decimal)),
            ("CapturedAmount", typeof(decimal)),
            ("RefundedAmount", typeof(decimal)),
            ("GuaranteeExpiresAt", typeof(DateTimeOffset?)),
            ("NextAction", typeof(CustomerActionView)),
            ("FailureCode", typeof(string)),
            ("FailureReason", typeof(string)),
            ("Version", typeof(long)),
            ("CreatedAt", typeof(DateTimeOffset)),
            ("UpdatedAt", typeof(DateTimeOffset))
        ]);

    [Fact]
    public void PaymentMethodOption_matches_the_master_contract()
        => AssertShape(typeof(PaymentMethodOptionView), BindingFlags.Public | BindingFlags.Instance,
        [
            ("Id", typeof(string)),
            ("TenderType", typeof(TenderType)),
            ("DisplayCode", typeof(string)),
            ("CurrencyId", typeof(int)),
            ("AvailableAmount", typeof(decimal?)),
            ("MinimumAmount", typeof(decimal?)),
            ("MaximumAmount", typeof(decimal?)),
            ("SupportsPartialAmount", typeof(bool)),
            ("IsDefault", typeof(bool)),
            ("CanAutoSelect", typeof(bool)),
            ("CustomerActionType", typeof(CustomerActionType)),
            ("AssuranceCapability", typeof(PaymentAssuranceCapability)),
            ("CaptureMode", typeof(PaymentCaptureMode)),
            ("ExpiresAt", typeof(DateTimeOffset?))
        ]);

    [Fact]
    public void PaymentSessionChanged_carries_exactly_the_master_fields_in_the_v1_namespace()
    {
        Assert.Equal("AeroTech.Messages.JetPay.IntegrationEvents.V1", typeof(PaymentSessionChanged).Namespace);
        Assert.True(typeof(PaymentSessionChanged).IsSubclassOf(typeof(BaseIntegrationEvent)));

        AssertShape(typeof(PaymentSessionChanged), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
        [
            ("PaymentSessionId", typeof(string)),
            ("PayableInstructionId", typeof(string)),
            ("OrderId", typeof(long)),
            ("CommercialVersion", typeof(int)),
            ("Purpose", typeof(PaymentPurpose)),
            ("Status", typeof(PaymentSessionStatus)),
            ("RequiredAmount", typeof(decimal)),
            ("GuaranteedAmount", typeof(decimal)),
            ("CapturedAmount", typeof(decimal)),
            ("RefundedAmount", typeof(decimal)),
            ("CurrencyId", typeof(int)),
            ("EarliestGuaranteeExpiry", typeof(DateTimeOffset?)),
            ("ExpiresAt", typeof(DateTimeOffset?)),
            ("Version", typeof(long)),
            ("FailureCode", typeof(string)),
            ("OccurredAt", typeof(DateTimeOffset))
        ]);
    }

    [Fact]
    public void PaymentPaidUnapplied_is_published_in_the_v1_namespace()
    {
        Assert.Equal("AeroTech.Messages.JetPay.IntegrationEvents.V1", typeof(PaymentPaidUnapplied).Namespace);
        Assert.True(typeof(PaymentPaidUnapplied).IsSubclassOf(typeof(BaseIntegrationEvent)));

        AssertShape(typeof(PaymentPaidUnapplied), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
        [
            ("PaymentSessionId", typeof(string)),
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
    public void Enumerations_match_the_master_contract()
    {
        Assert.Equal(["Customer", "Agency", "Corporate", "Partner"], Enum.GetNames<PayerType>());
        Assert.Equal(["CustomerInteractive", "UnattendedApi", "StaffAssisted"], Enum.GetNames<PaymentInteractionMode>());
        Assert.Equal(["Explicit", "Default"], Enum.GetNames<PaymentSelectionMode>());
        Assert.Equal(["FundsReceived", "IssuanceGuaranteed"], Enum.GetNames<PaymentAssuranceRequirement>());
        Assert.Equal(["FundsReceived", "CommitmentToPay"], Enum.GetNames<PaymentAssuranceCapability>());
        Assert.Equal(
            ["Created", "RequiresPaymentMethod", "RequiresCustomerAction", "Processing", "PartiallyCovered", "Guaranteed", "Paid", "Cancelled", "Expired"],
            Enum.GetNames<PaymentSessionStatus>());
        Assert.Equal(
            ["Created", "RequiresCustomerAction", "Processing", "Authorized", "PartiallyCaptured", "Captured", "Failed", "Cancelled", "Expired"],
            Enum.GetNames<PaymentIntentStatus>());
        Assert.Equal(
            ["IranianPgw", "ExternalCard", "AccountToAccount", "Bnpl", "StoredValue", "AgencyDeposit", "CustomerCredit", "AgencyCredit", "CorporateCredit", "Cash", "BankTransferReference", "Voucher", "LoyaltyPoints"],
            Enum.GetNames<TenderType>());
        Assert.Equal(["InitialSale", "AddService", "ExchangeAdditionalCollection", "GroupDeposit", "FinalPayment", "Other"], Enum.GetNames<PaymentPurpose>());
        Assert.Equal(["None", "Redirect", "HtmlForm", "Sdk", "ThreeDsChallenge"], Enum.GetNames<CustomerActionType>());
        Assert.Equal(["Automatic", "Manual"], Enum.GetNames<PaymentCaptureMode>());
    }

    [Theory]
    [InlineData(typeof(CreatePaymentSessionCommand))]
    [InlineData(typeof(ConfirmPaymentSessionCommand))]
    [InlineData(typeof(PaymentSelection))]
    [InlineData(typeof(ResolvePaymentMethodOptionsQuery))]
    public void Ordering_facing_requests_never_carry_capture_mode_guarantee_or_provider(Type request)
        => Assert.DoesNotContain(
            request.GetProperties().Select(property => property.Name),
            name => name is "CaptureMode" or "RequiredGuarantee" or "ProviderCode" or "ProviderReference");

    private static void AssertShape(Type type, BindingFlags flags, IReadOnlyList<(string Name, Type Type)> expected)
    {
        var actual = type.GetProperties(flags).Select(property => (property.Name, Type: property.PropertyType)).ToList();

        Assert.Equal(expected.Select(field => field.Name), actual.Select(field => field.Name));
        Assert.Equal(expected.Select(field => field.Type), actual.Select(field => field.Type));
    }
}
