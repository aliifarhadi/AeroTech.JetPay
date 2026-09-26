using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fakes;

public sealed class RecordingOutboxWriter : IOutboxWriter
{
    private readonly List<object> _written = [];

    public IReadOnlyList<object> Written => _written;

    public Task WriteAsync(object message, IDomainEvent source, CancellationToken cancellationToken = default)
    {
        if (message is BaseIntegrationEvent integrationEvent)
        {
            integrationEvent.EventId = source.EventId;
            integrationEvent.AggregateId = source.AggregateId;
            integrationEvent.TimeOfOccurrence = source.TimeOfOccurrence;
            integrationEvent.SourceSystem = "JetPay";
        }

        _written.Add(message);
        return Task.CompletedTask;
    }
}
