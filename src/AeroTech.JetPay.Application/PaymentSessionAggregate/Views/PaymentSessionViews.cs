using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Views
{
    public sealed record PaymentSessionResponse(PaymentSessionView Session, IReadOnlyList<PaymentIntentView> PaymentIntents);

    public sealed record PaymentSessionView(
        string Id,
        string PayableInstructionId,
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PayerType PayerType,
        long PayerId,
        PaymentInitiatorContextView InitiatorContext,
        decimal RequiredAmount,
        int CurrencyId,
        PaymentAssuranceRequirement AssuranceRequirement,
        PaymentInteractionMode InteractionMode,
        PaymentSessionStatus Status,
        decimal GuaranteedAmount,
        decimal CapturedAmount,
        decimal RefundedAmount,
        decimal OutstandingAmount,
        DateTimeOffset? EarliestGuaranteeExpiry,
        DateTimeOffset? ExpiresAt,
        string? FailureCode,
        long Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        IReadOnlyList<string> PaymentIntentIds);

    public sealed record PaymentInitiatorContextView(
        SalesChannel SalesChannel,
        string ActorType,
        long ActorId,
        long? OfficeId);

    public sealed record PaymentIntentView(
        string Id,
        string PaymentSessionId,
        int Sequence,
        string PaymentMethodOptionId,
        TenderType TenderType,
        decimal RequestedAmount,
        int CurrencyId,
        PaymentCaptureMode CaptureMode,
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
}
