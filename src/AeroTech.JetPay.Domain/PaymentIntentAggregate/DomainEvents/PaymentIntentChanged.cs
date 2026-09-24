using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.PaymentIntentAggregate.DomainEvents
{
    public sealed record PaymentIntentChanged(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        string PaymentIntentId,
        string PayableInstructionId,
        long OrderId,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PaymentIntentStatus Status,
        RequiredGuarantee RequiredGuarantee,
        PaymentCaptureMode CaptureMode,
        decimal RequestedAmount,
        decimal AuthorizedAmount,
        decimal GuaranteedAmount,
        decimal CapturedAmount,
        int CurrencyId,
        DateTimeOffset? GuaranteeExpiresAt,
        DateTimeOffset? IntentExpiresAt,
        long Version,
        string? FailureCode,
        DateTimeOffset OccurredAt) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
