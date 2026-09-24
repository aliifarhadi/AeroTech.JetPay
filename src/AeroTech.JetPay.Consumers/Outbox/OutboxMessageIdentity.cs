using AeroTech.Messages;

namespace AeroTech.JetPay.Consumers.Outbox
{
    /// <summary>
    /// Derives the transport MessageId from the message's own identity, so republishing an outbox row after a crash
    /// carries the same MessageId and consumers' inbox deduplication discards the copy.
    /// </summary>
    public static class OutboxMessageIdentity
    {
        public static Guid? Of(object payload) => payload switch
        {
            BaseIntegrationEvent integrationEvent when long.TryParse(integrationEvent.EventId, out var eventId) => FromEventId(eventId),
            BaseAcknowledgeCommand command => command.CommandId,
            BaseCommand command when long.TryParse(command.EventId, out var eventId) => FromEventId(eventId),
            _ => null
        };

        private static Guid FromEventId(long eventId)
        {
            Span<byte> bytes = stackalloc byte[16];
            BitConverter.TryWriteBytes(bytes, eventId);
            return new Guid(bytes);
        }
    }
}
