using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Views
{
    public sealed record PaymentSessionView(
        string Id,
        string PayableInstructionId,
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PaymentPurpose Purpose,
        long IssuerLegalEntityId,
        PayerType PayerType,
        long PayerId,
        PaymentInitiatorContextView Initiator,
        PaymentInteractionMode InteractionMode,
        PaymentSelectionMode SelectionMode,
        decimal RequiredAmount,
        int CurrencyId,
        PaymentAssuranceRequirement AssuranceRequirement,
        PaymentSessionStatus Status,
        decimal GuaranteedAmount,
        decimal CapturedAmount,
        decimal OutstandingAmount,
        DateTimeOffset? ExpiresAt,
        string? FailureCode,
        long Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        IReadOnlyList<PaymentIntentView> Intents);

    public sealed record PaymentInitiatorContextView(
        string ActorType,
        long ActorId,
        SalesChannel SalesChannel,
        long? OfficeId);

    public sealed record PaymentIntentView(
        string Id,
        string PaymentSessionId,
        string PaymentMethodOptionId,
        TenderType TenderType,
        decimal RequestedAmount,
        int CurrencyId,
        PaymentIntentStatus Status,
        decimal AuthorizedAmount,
        decimal GuaranteedAmount,
        decimal CapturedAmount,
        decimal RefundedAmount,
        DateTimeOffset? GuaranteeExpiresAt,
        CustomerActionView? NextAction,
        string? FailureCode,
        string? FailureReason,
        long Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public sealed record CustomerActionView(
        CustomerActionType Type,
        string? Url,
        string? HttpMethod,
        IReadOnlyDictionary<string, string>? FormFields,
        DateTimeOffset? ExpiresAt);

    public sealed record PaymentSelection(string PaymentMethodOptionId, decimal Amount);
}
