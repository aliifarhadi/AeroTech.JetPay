using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application._Shared.Events;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.JetPay.IntegrationEvents.V1.PaymentIntentChangedV1;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.EventHandlers
{
    public sealed class PublishPaymentIntentChangedIntegrationEvent : INotificationHandler<DomainEventNotification<PaymentIntentChanged>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishPaymentIntentChangedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<PaymentIntentChanged> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.PaymentIntentId,
                @event.PayableInstructionId,
                @event.OrderId,
                @event.CommercialVersion,
                @event.Purpose,
                @event.Status,
                @event.RequiredGuarantee,
                @event.CaptureMode,
                @event.RequestedAmount,
                @event.AuthorizedAmount,
                @event.GuaranteedAmount,
                @event.CapturedAmount,
                @event.CurrencyId,
                @event.GuaranteeExpiresAt,
                @event.IntentExpiresAt,
                @event.Version,
                @event.FailureCode,
                @event.OccurredAt), @event, cancellationToken);
        }
    }
}
