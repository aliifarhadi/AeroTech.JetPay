using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;

namespace AeroTech.JetPay.Application._Shared.Idempotency
{
    public interface IIdempotencyGuard
    {
        /// <summary>
        /// Returns the payment intent id of an earlier committed request with the same key and payload, or null when
        /// the key is new. The same key with a different payload is rejected.
        /// </summary>
        Task<string?> FindReplayAsync(
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            string requestFingerprint,
            CancellationToken cancellationToken = default);

        Task RecordAsync(
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            string requestFingerprint,
            string paymentIntentId,
            CancellationToken cancellationToken = default);
    }

    public sealed class IdempotencyGuard : IIdempotencyGuard
    {
        private readonly IIdempotencyRecordRepository _records;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public IdempotencyGuard(IIdempotencyRecordRepository records, IIdGenerator idGenerator, IClock clock)
        {
            _records = records;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<string?> FindReplayAsync(
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            string requestFingerprint,
            CancellationToken cancellationToken = default)
        {
            var record = await _records.FindAsync(operation, scope, idempotencyKey, cancellationToken);

            if (record is null)
                return null;

            if (!record.Matches(requestFingerprint))
                throw ExceptionFactory.IdempotencyKeyReusedWithDifferentPayload(idempotencyKey, operation);

            return record.PaymentIntentId;
        }

        public Task RecordAsync(
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            string requestFingerprint,
            string paymentIntentId,
            CancellationToken cancellationToken = default)
            => _records.AddAsync(
                IdempotencyRecord.Create(
                    _idGenerator.NewId(),
                    operation,
                    scope,
                    idempotencyKey,
                    requestFingerprint,
                    paymentIntentId,
                    _clock.GetDateTime()),
                cancellationToken);
    }
}
