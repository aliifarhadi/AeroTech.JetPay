using AeroTech.Framework.Core.Domain.Aggregates;

namespace AeroTech.JetPay.Domain.IdempotencyRecordAggregate
{
    /// <summary>
    /// Durable evidence that one idempotent mutation was committed. It is saved in the same unit of work as the
    /// payment intent change it guards, so a replay can never produce a second financial effect.
    /// </summary>
    public sealed class IdempotencyRecord : AggregateRoot<long>
    {
        public const string GlobalScope = "*";

        private IdempotencyRecord()
        {
        }

        private IdempotencyRecord(
            long id,
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            string requestFingerprint,
            string paymentIntentId,
            DateTimeOffset createdAt)
        {
            Id = id;
            Operation = operation;
            Scope = scope;
            IdempotencyKey = idempotencyKey;
            RequestFingerprint = requestFingerprint;
            PaymentIntentId = paymentIntentId;
            CreatedAt = createdAt;
        }

        public IdempotentOperation Operation { get; private set; }

        public string Scope { get; private set; } = default!;

        public string IdempotencyKey { get; private set; } = default!;

        public string RequestFingerprint { get; private set; } = default!;

        public string PaymentIntentId { get; private set; } = default!;

        public DateTimeOffset CreatedAt { get; private set; }

        public static IdempotencyRecord Create(
            long id,
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            string requestFingerprint,
            string paymentIntentId,
            DateTimeOffset createdAt)
            => new(id, operation, scope, idempotencyKey, requestFingerprint, paymentIntentId, createdAt);

        public bool Matches(string requestFingerprint) => RequestFingerprint == requestFingerprint;
    }
}
