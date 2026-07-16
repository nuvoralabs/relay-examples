using Nuvora.Nexus.Relay.Core.Coordination;

namespace Inventory.Locking;

/// <summary>
/// An in-memory <see cref="IDistributedLock"/> that enforces real mutual exclusion on the
/// transaction-scoped path, so the serialization contract of <see cref="DistributedAggregateWriteLock"/>
/// can be proven without Postgres. On the Postgres backend the lock is a transaction-scoped advisory lock
/// released when the unit-of-work transaction commits or rolls back; here we model that with a set of
/// currently-held resource keys plus an explicit <see cref="EndTransaction"/> to stand in for commit/rollback.
///
/// <para><see cref="TryAcquireForCurrentTransactionAsync"/> grants only if no one else holds the key (never
/// waits). <see cref="AcquireForCurrentTransactionAsync"/> waits until the key is free, modelling the
/// blocking acquire.</para>
/// </summary>
internal sealed class FakeDistributedLock : IDistributedLock
{
    private readonly object _gate = new();
    private readonly HashSet<string> _held = new();

    /// <summary>Every resource key any caller asked to lock, in order — lets a test assert namespacing.</summary>
    public List<string> RequestedResources { get; } = new();

    public Task<bool> TryAcquireForCurrentTransactionAsync(string resource, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            RequestedResources.Add(resource);
            // Already held by *this* transaction-modelling fake? Re-entrant grant. Held by another? Deny.
            return Task.FromResult(_held.Add(resource));
        }
    }

    public async Task AcquireForCurrentTransactionAsync(string resource, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            RequestedResources.Add(resource);
        }

        // Spin-wait until the key is free, then take it — the blocking-acquire contract.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_held.Add(resource))
                {
                    return;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
        }
    }

    /// <summary>Models the unit-of-work transaction committing or rolling back: every lock it held is released.</summary>
    public void EndTransaction(params string[] resources)
    {
        lock (_gate)
        {
            foreach (var resource in resources)
            {
                _held.Remove(resource);
            }
        }
    }

    /// <summary>Whether the given resource key is currently locked.</summary>
    public bool IsHeld(string resource)
    {
        lock (_gate)
        {
            return _held.Contains(resource);
        }
    }

    public Task<IDistributedLockHandle?> TryAcquireAsync(string resource, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This sample exercises only the transaction-scoped (per-aggregate write lock) path.");
}
