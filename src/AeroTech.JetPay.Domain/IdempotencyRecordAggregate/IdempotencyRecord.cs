using AeroTech.Framework.Core.Domain.Aggregates;

namespace AeroTech.JetPay.Domain.IdempotencyRecordAggregate
{
    public sealed class IdempotencyRecord : AggregateRoot<long>
    {
        public const string GlobalScope = "*";

        private readonly List<string> _paymentIntentIds = new();

        private IdempotencyRecord()
        {
        }

        private IdempotencyRecord(
            long id,
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            string requestFingerprint,
            string paymentSessionId,
            IEnumerable<string> paymentIntentIds,
            DateTimeOffset createdAt)
        {
            Id = id;
            Operation = operation;
            Scope = scope;
            IdempotencyKey = idempotencyKey;
            RequestFingerprint = requestFingerprint;
            PaymentSessionId = paymentSessionId;
            _paymentIntentIds.AddRange(paymentIntentIds);
            CreatedAt = createdAt;
        }

        public IdempotentOperation Operation { get; private set; }

        public string Scope { get; private set; } = default!;

        public string IdempotencyKey { get; private set; } = default!;

        public string RequestFingerprint { get; private set; } = default!;

        public string PaymentSessionId { get; private set; } = default!;

        public IReadOnlyCollection<string> PaymentIntentIds => _paymentIntentIds.AsReadOnly();

        public DateTimeOffset CreatedAt { get; private set; }

        public static IdempotencyRecord Create(
            long id,
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            string requestFingerprint,
            string paymentSessionId,
            IEnumerable<string> paymentIntentIds,
            DateTimeOffset createdAt)
            => new(id, operation, scope, idempotencyKey, requestFingerprint, paymentSessionId, paymentIntentIds, createdAt);

        public bool Matches(string requestFingerprint) => RequestFingerprint == requestFingerprint;
    }
}
