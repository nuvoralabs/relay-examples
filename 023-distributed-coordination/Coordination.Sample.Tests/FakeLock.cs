using Nuvora.Nexus.Relay.Core.Coordination;

namespace Coordination.Sample;

/// <summary>
/// A scripted in-memory <see cref="IDistributedLock"/> so leader election can be tested without Postgres.
/// It grants or denies session-lock acquisition per a queued script, and grants once the script is
/// exhausted. <see cref="AcquireAttempts"/> lets a test assert it retried.
/// </summary>
internal sealed class FakeLock : IDistributedLock
{
    private readonly Queue<bool> _grants;

    public FakeLock(params bool[] grants) => _grants = new Queue<bool>(grants);

    public int AcquireAttempts { get; private set; }

    public Task<IDistributedLockHandle?> TryAcquireAsync(string resource, CancellationToken cancellationToken = default)
    {
        AcquireAttempts++;
        var grant = _grants.Count > 0 ? _grants.Dequeue() : true;
        return Task.FromResult<IDistributedLockHandle?>(grant ? new Handle(resource) : null);
    }

    public Task<bool> TryAcquireForCurrentTransactionAsync(string resource, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task AcquireForCurrentTransactionAsync(string resource, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    private sealed class Handle : IDistributedLockHandle
    {
        public Handle(string resource) => Resource = resource;
        public string Resource { get; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
