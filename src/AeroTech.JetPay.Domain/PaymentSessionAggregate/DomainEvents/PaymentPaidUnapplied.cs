using AeroTech.Framework.Core.Domain.Events;

namespace AeroTech.JetPay.Domain.PaymentSessionAggregate.DomainEvents
{
    public sealed record PaymentPaidUnapplied(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        string PaymentSessionId,
        string PaymentIntentId,
        long OrderId,
        string SupersededPayableInstructionId,
        decimal CapturedAmount,
        int CurrencyId,
        string ReasonCode,
        DateTimeOffset OccurredAt) : DomainEvent(EventId, AggregateId, TimeOfOccurrence);
}
