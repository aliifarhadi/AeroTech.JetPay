using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fakes;

public sealed class InMemoryUnitOfWork(IDomainEventDispatcher dispatcher) : IUnitOfWork
{
    private readonly List<Action> _pending = [];
    private readonly HashSet<IAggregateRoot> _tracked = new(ReferenceEqualityComparer.Instance);

    public int SaveCount { get; private set; }

    public void Stage(Action commit) => _pending.Add(commit);

    public void Track(IAggregateRoot aggregate) => _tracked.Add(aggregate);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var events = _tracked.SelectMany(aggregate => aggregate.GetEvents()).ToList();

        foreach (var aggregate in _tracked)
            aggregate.ClearEvents();

        await dispatcher.DispatchAsync(events, cancellationToken);

        var committed = _pending.Count;

        foreach (var commit in _pending)
            commit();

        _pending.Clear();
        SaveCount++;

        return committed;
    }
}
