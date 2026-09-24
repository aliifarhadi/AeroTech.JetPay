using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fakes;

public sealed class InMemoryDistributedLock : IDistributedLock
{
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);

    public void Hold(string resource) => _held.Add(resource);

    public Task<IAsyncDisposable?> AcquireAsync(string resource, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        if (!_held.Add(resource))
            return Task.FromResult<IAsyncDisposable?>(null);

        return Task.FromResult<IAsyncDisposable?>(new Handle(() => _held.Remove(resource)));
    }

    private sealed class Handle(Action release) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            release();
            return ValueTask.CompletedTask;
        }
    }
}
