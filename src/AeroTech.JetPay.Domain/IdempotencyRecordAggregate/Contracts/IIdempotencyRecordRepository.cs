namespace AeroTech.JetPay.Domain.IdempotencyRecordAggregate.Contracts
{
    public interface IIdempotencyRecordRepository
    {
        Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);

        Task<IdempotencyRecord?> FindAsync(
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            CancellationToken cancellationToken = default);
    }
}
