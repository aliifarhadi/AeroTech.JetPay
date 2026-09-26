using System.Reflection;
using AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.AddPaymentSelections;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.Messages;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.JetPay.IntegrationEvents.V1;
using AeroTech.Messages.Shared.Enums;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.Contract;

public sealed class ContractConformanceTests
{
    [Fact]
    public void PaymentSession_exposes_the_master_fields_plus_failure_code()
        => AssertShape(typeof(PaymentSessionView), BindingFlags.Public | BindingFlags.Instance,
        [
            ("Id", typeof(string)),
            ("PayableInstructionId", typeof(string)),
            ("OrderId", typeof(long)),
            ("OrderReference", typeof(string)),
            ("CommercialVersion", typeof(int)),
            ("Purpose", typeof(PaymentPurpose)),
            ("IssuerLegalEntityId", typeof(long)),
            ("PayerType", typeof(PayerType)),
            ("PayerId", typeof(long)),
            ("Initiator", typeof(PaymentInitiatorContextView)),
            ("InteractionMode", typeof(PaymentInteractionMode)),
            ("SelectionMode", typeof(PaymentSelectionMode)),
            ("RequiredAmount", typeof(decimal)),
            ("CurrencyId", typeof(int)),
            ("AssuranceRequirement", typeof(PaymentAssuranceRequirement)),
            ("Status", typeof(PaymentSessionStatus)),
            ("GuaranteedAmount", typeof(decimal)),
            ("CapturedAmount", typeof(decimal)),
            ("OutstandingAmount", typeof(decimal)),
            ("ExpiresAt", typeof(DateTimeOffset?)),
            ("FailureCode", typeof(string)),
            ("Version", typeof(long)),
            ("CreatedAt", typeof(DateTimeOffset)),
            ("UpdatedAt", typeof(DateTimeOffset)),
            ("Intents", typeof(IReadOnlyList<PaymentIntentView>))
        ]);

    [Fact]
    public void PaymentInitiatorContext_matches_the_master_contract()
        => AssertShape(typeof(PaymentInitiatorContextView), BindingFlags.Public | BindingFlags.Instance,
        [
            ("ActorType", typeof(string)),
            ("ActorId", typeof(long)),
            ("SalesChannel", typeof(SalesChannel)),
            ("OfficeId", typeof(long?))
        ]);

    [Fact]
    public void PaymentIntent_exposes_the_master_fields_and_keeps_provider_attempts_internal()
        => AssertShape(typeof(PaymentIntentView), BindingFlags.Public | BindingFlags.Instance,
        [
            ("Id", typeof(string)),
            ("PaymentSessionId", typeof(string)),
            ("PaymentMethodOptionId", typeof(string)),
            ("TenderType", typeof(TenderType)),
            ("RequestedAmount", typeof(decimal)),
            ("CurrencyId", typeof(int)),
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
            ("SupportedAssuranceRequirements", typeof(IReadOnlyList<PaymentAssuranceRequirement>)),
            ("ExpiresAt", typeof(DateTimeOffset?))
        ]);

    [Fact]
    public void Resolve_and_create_requests_carry_the_master_fields()
    {
        Assert.Equal(
            ["OrderId", "OrderReference", "CommercialVersion", "Purpose", "IssuerLegalEntityId", "PayerType", "PayerId", "Initiator", "InteractionMode", "Amount", "CurrencyId", "AssuranceRequirement"],
            typeof(ResolvePaymentMethodOptionsQuery).GetProperties().Select(property => property.Name));

        Assert.Equal(
            ["IdempotencyKey", "PayableInstructionId", "OrderId", "OrderReference", "CommercialVersion", "Purpose", "IssuerLegalEntityId", "PayerType", "PayerId", "Initiator", "InteractionMode", "SelectionMode", "RequiredAmount", "CurrencyId", "AssuranceRequirement", "ExpiresAt", "Selections"],
            typeof(CreatePaymentSessionCommand).GetProperties().Select(property => property.Name));

        Assert.Equal(
            ["IdempotencyKey", "PaymentSessionId", "Selections", "ReturnUrl"],
            typeof(AddPaymentSelectionsCommand).GetProperties().Select(property => property.Name));

        Assert.Equal(["PaymentMethodOptionId", "Amount"], typeof(PaymentSelection).GetProperties().Select(property => property.Name));
    }

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
            ("AssuranceRequirement", typeof(PaymentAssuranceRequirement)),
            ("RequiredAmount", typeof(decimal)),
            ("GuaranteedAmount", typeof(decimal)),
            ("CapturedAmount", typeof(decimal)),
            ("OutstandingAmount", typeof(decimal)),
            ("CurrencyId", typeof(int)),
            ("ExpiresAt", typeof(DateTimeOffset?)),
            ("Version", typeof(long)),
            ("FailureCode", typeof(string)),
            ("OccurredAt", typeof(DateTimeOffset))
        ]);
    }

    [Fact]
    public void PaymentPaidUnapplied_carries_exactly_the_master_fields_in_the_v1_namespace()
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
        Assert.Equal(["Interactive", "Default", "Explicit"], Enum.GetNames<PaymentSelectionMode>());
        Assert.Equal(["FundsReceived", "IssuanceGuaranteed"], Enum.GetNames<PaymentAssuranceRequirement>());
        Assert.Equal(
            ["Created", "RequiresPaymentMethod", "Processing", "PartiallyFunded", "Guaranteed", "Paid", "Failed", "Cancelled", "Expired"],
            Enum.GetNames<PaymentSessionStatus>());
        Assert.Equal(
            ["Created", "RequiresCustomerAction", "Processing", "Authorized", "PartiallyCaptured", "Captured", "Failed", "Cancelled", "Expired"],
            Enum.GetNames<PaymentIntentStatus>());
        Assert.Equal(
            ["Created", "CustomerActionPending", "CallbackReceived", "VerificationPending", "Verified", "Settled", "AutoReversalPending", "Reversed", "Failed", "Unknown"],
            Enum.GetNames<ProviderPaymentAttemptStatus>());
        Assert.Equal(
            ["IranianPgw", "ExternalCard", "Bnpl", "StoredValue", "AgencyDeposit", "CustomerCredit", "AgencyCredit", "CorporateCredit", "BankTransferReference", "Cash", "PosTerminal"],
            Enum.GetNames<TenderType>());
        Assert.Equal(["InitialSale", "AddService", "ExchangeAdditionalCollection", "GroupDeposit", "FinalPayment", "Other"], Enum.GetNames<PaymentPurpose>());
        Assert.Equal(["None", "Redirect", "HtmlForm", "Sdk", "ThreeDsChallenge"], Enum.GetNames<CustomerActionType>());
    }

    [Theory]
    [InlineData(typeof(CreatePaymentSessionCommand))]
    [InlineData(typeof(AddPaymentSelectionsCommand))]
    [InlineData(typeof(PaymentSelection))]
    [InlineData(typeof(ResolvePaymentMethodOptionsQuery))]
    [InlineData(typeof(PaymentMethodOptionView))]
    public void Ordering_facing_contracts_never_carry_capture_mode_or_provider_routes(Type contract)
        => Assert.DoesNotContain(
            contract.GetProperties().Select(property => property.Name),
            name => name is "CaptureMode" or "AssuranceCapability" or "ProviderProfileId" or "ProviderCode" or "ProviderReference" or "FundingReference");

    private static void AssertShape(Type type, BindingFlags flags, IReadOnlyList<(string Name, Type Type)> expected)
    {
        var actual = type.GetProperties(flags).Select(property => (property.Name, Type: property.PropertyType)).ToList();

        Assert.Equal(expected.Select(field => field.Name), actual.Select(field => field.Name));
        Assert.Equal(expected.Select(field => field.Type), actual.Select(field => field.Type));
    }
}
