using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;

namespace AeroTech.JetPay.Application._Shared.Idempotency
{
    public interface IIdempotencyGuard
    {
        Task<IdempotencyRecord?> FindReplayAsync(
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
            string paymentSessionId,
            IEnumerable<string> paymentIntentIds,
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

        public async Task<IdempotencyRecord?> FindReplayAsync(
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            string requestFingerprint,
            CancellationToken cancellationToken = default)
        {
            var record = await _records.FindAsync(operation, scope, idempotencyKey, cancellationToken);

            if (record is not null && !record.Matches(requestFingerprint))
                throw ExceptionFactory.IdempotencyKeyReusedWithDifferentPayload(idempotencyKey, operation);

            return record;
        }

        public Task RecordAsync(
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            string requestFingerprint,
            string paymentSessionId,
            IEnumerable<string> paymentIntentIds,
            CancellationToken cancellationToken = default)
            => _records.AddAsync(
                IdempotencyRecord.Create(
                    _idGenerator.NewId(),
                    operation,
                    scope,
                    idempotencyKey,
                    requestFingerprint,
                    paymentSessionId,
                    paymentIntentIds,
                    _clock.GetDateTime()),
                cancellationToken);
    }
}
