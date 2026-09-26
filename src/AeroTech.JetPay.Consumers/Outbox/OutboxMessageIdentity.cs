using AeroTech.Messages;

namespace AeroTech.JetPay.Consumers.Outbox
{
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
