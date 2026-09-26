using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.PaymentSessionAggregate.DomainEvents
{
    public sealed record PaymentSessionChanged(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        string PaymentSessionId,
        string PayableInstructionId,
        long OrderId,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PaymentSessionStatus Status,
        PaymentAssuranceRequirement AssuranceRequirement,
        decimal RequiredAmount,
        decimal GuaranteedAmount,
        decimal CapturedAmount,
        decimal OutstandingAmount,
        int CurrencyId,
        DateTimeOffset? ExpiresAt,
        long Version,
        string? FailureCode,
        DateTimeOffset OccurredAt) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
