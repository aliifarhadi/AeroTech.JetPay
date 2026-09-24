using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.JetPay.Persistence.IdempotencyRecordAggregate
{
    public sealed class IdempotencyRecordRepository : IIdempotencyRecordRepository
    {
        private readonly JetPayDbContext _dbContext;

        public IdempotencyRecordRepository(JetPayDbContext dbContext) => _dbContext = dbContext;

        public async Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
            => await _dbContext.IdempotencyRecords.AddAsync(record, cancellationToken);

        public Task<IdempotencyRecord?> FindAsync(
            IdempotentOperation operation,
            string scope,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => _dbContext.IdempotencyRecords.FirstOrDefaultAsync(
                record => record.Operation == operation && record.Scope == scope && record.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }
}
