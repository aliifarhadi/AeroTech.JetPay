using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate.Contracts;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fakes;

public sealed class InMemoryIdempotencyRecordRepository(InMemoryUnitOfWork unitOfWork) : IIdempotencyRecordRepository
{
    private readonly List<IdempotencyRecord> _committed = [];

    public IReadOnlyList<IdempotencyRecord> Committed => _committed;

    public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        unitOfWork.Stage(() => _committed.Add(record));
        return Task.CompletedTask;
    }

    public Task<IdempotencyRecord?> FindAsync(
        IdempotentOperation operation,
        string scope,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_committed.FirstOrDefault(record =>
            record.Operation == operation && record.Scope == scope && record.IdempotencyKey == idempotencyKey));
}
