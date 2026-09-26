using AeroTech.Framework.Core.Domain.ValueObjects;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects
{
    public sealed class PaymentInitiatorContext : ValueObject
    {
        private PaymentInitiatorContext()
        {
        }

        public PaymentInitiatorContext(SalesChannel salesChannel, string actorType, long actorId, long? officeId)
        {
            SalesChannel = salesChannel;
            ActorType = actorType;
            ActorId = actorId;
            OfficeId = officeId;
        }

        public SalesChannel SalesChannel { get; private set; }

        public string ActorType { get; private set; } = default!;

        public long ActorId { get; private set; }

        public long? OfficeId { get; private set; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return SalesChannel;
            yield return ActorType;
            yield return ActorId;
            yield return OfficeId;
        }
    }
}
