using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application._Shared.Events;
using MediatR;
using DomainPaidUnapplied = AeroTech.JetPay.Domain.PaymentSessionAggregate.DomainEvents.PaymentPaidUnapplied;
using DomainSessionChanged = AeroTech.JetPay.Domain.PaymentSessionAggregate.DomainEvents.PaymentSessionChanged;
using PaidUnappliedEvent = AeroTech.Messages.JetPay.IntegrationEvents.V1.PaymentPaidUnapplied;
using SessionChangedEvent = AeroTech.Messages.JetPay.IntegrationEvents.V1.PaymentSessionChanged;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.EventHandlers
{
    public sealed class PublishPaymentSessionChangedIntegrationEvent : INotificationHandler<DomainEventNotification<DomainSessionChanged>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishPaymentSessionChangedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<DomainSessionChanged> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new SessionChangedEvent(
                @event.PaymentSessionId,
                @event.PayableInstructionId,
                @event.OrderId,
                @event.CommercialVersion,
                @event.Purpose,
                @event.Status,
                @event.AssuranceRequirement,
                @event.RequiredAmount,
                @event.GuaranteedAmount,
                @event.CapturedAmount,
                @event.OutstandingAmount,
                @event.CurrencyId,
                @event.ExpiresAt,
                @event.Version,
                @event.FailureCode,
                @event.OccurredAt), @event, cancellationToken);
        }
    }

    public sealed class PublishPaymentPaidUnappliedIntegrationEvent : INotificationHandler<DomainEventNotification<DomainPaidUnapplied>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishPaymentPaidUnappliedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<DomainPaidUnapplied> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new PaidUnappliedEvent(
                @event.PaymentSessionId,
                @event.PaymentIntentId,
                @event.OrderId,
                @event.SupersededPayableInstructionId,
                @event.CapturedAmount,
                @event.CurrencyId,
                @event.ReasonCode,
                @event.OccurredAt), @event, cancellationToken);
        }
    }
}
