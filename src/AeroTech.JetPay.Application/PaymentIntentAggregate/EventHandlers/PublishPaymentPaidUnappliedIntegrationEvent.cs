using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application._Shared.Events;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.JetPay.IntegrationEvents.V1.PaymentPaidUnappliedV1;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.EventHandlers
{
    public sealed class PublishPaymentPaidUnappliedIntegrationEvent : INotificationHandler<DomainEventNotification<PaymentPaidUnapplied>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishPaymentPaidUnappliedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<PaymentPaidUnapplied> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
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
