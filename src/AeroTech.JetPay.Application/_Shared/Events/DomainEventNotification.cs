using AeroTech.Framework.Core.Domain.Events;
using MediatR;

namespace AeroTech.JetPay.Application._Shared.Events
{
    public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
        where TDomainEvent : IDomainEvent;
}
