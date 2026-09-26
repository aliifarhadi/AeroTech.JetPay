using AeroTech.Framework.Core.Domain.ValueObjects;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects
{
    public sealed class PaymentInitiatorContext : ValueObject
    {
        private PaymentInitiatorContext()
        {
        }

        public PaymentInitiatorContext(string actorType, long actorId, SalesChannel salesChannel, long? officeId)
        {
            ActorType = actorType;
            ActorId = actorId;
            SalesChannel = salesChannel;
            OfficeId = officeId;
        }

        public string ActorType { get; private set; } = default!;

        public long ActorId { get; private set; }

        public SalesChannel SalesChannel { get; private set; }

        public long? OfficeId { get; private set; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return ActorType;
            yield return ActorId;
            yield return SalesChannel;
            yield return OfficeId;
        }
    }
}
