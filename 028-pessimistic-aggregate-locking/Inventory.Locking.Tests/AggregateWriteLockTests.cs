using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Coordination;
using Xunit;

namespace Inventory.Locking;

/// <summary>
/// <see cref="DistributedAggregateWriteLock"/> is a <em>pessimistic</em> per-aggregate write lock: instead
/// of letting two writers to the same aggregate race and one fail with a concurrency conflict (the
/// optimistic default — see article 006), the second writer <strong>waits</strong> for the first. It does
/// this by taking a transaction-scoped distributed lock on a stable, per-aggregate-id resource key
/// (<c>relay:aggregate:{type}:{id}</c>), so the lock rides the command's unit-of-work transaction and
/// releases at commit/rollback.
///
/// <para>These tests drive it with a <see cref="FakeDistributedLock"/> that enforces real mutual exclusion
/// in memory, proving the serialization contract with no database. We model a busy SKU as the hot
/// aggregate — many concurrent reservations against the same stock item are exactly where pessimistic
/// locking earns its keep over a retry loop.</para>
/// </summary>
public sealed class AggregateWriteLockTests
{
    private const string AggregateType = "StockItem";

    // The framework builds this key internally; we assert against it the way the fake observes it.
    private static string ResourceKey(Guid id) => $"relay:aggregate:{AggregateType}:{id:N}";

    [Fact]
    public async Task Two_writers_to_the_same_aggregate_serialize()
    {
        var fake = new FakeDistributedLock();
        var sut = new DistributedAggregateWriteLock(fake);
        var sku = Guid.NewGuid();

        // Writer A takes the lock for its transaction (and holds it — the transaction has not ended).
        (await sut.TryAcquireAsync(AggregateType, sku)).Should().BeTrue("no one else holds the lock yet");

        // Writer B contends for the SAME aggregate while A still holds it: the no-wait try is denied.
        (await sut.TryAcquireAsync(AggregateType, sku))
            .Should().BeFalse("a second writer to the same aggregate id must not get the lock concurrently");

        // A blocking acquire WAITS rather than failing. Prove it does not complete while A holds the lock…
        var writerB = sut.AcquireAsync(AggregateType, sku);
        (await Task.WhenAny(writerB, Task.Delay(200))).Should().NotBe(writerB, "B must wait while A holds the lock");

        // …then A's transaction commits/rolls back, releasing the lock, and B proceeds.
        fake.EndTransaction(ResourceKey(sku));
        (await Task.WhenAny(writerB, Task.Delay(5000))).Should().Be(writerB, "B acquires once A releases on commit/rollback");
        await writerB;
    }

    [Fact]
    public async Task Writers_to_different_aggregates_do_not_block_each_other()
    {
        var fake = new FakeDistributedLock();
        var sut = new DistributedAggregateWriteLock(fake);

        var skuA = Guid.NewGuid();
        var skuB = Guid.NewGuid();

        (await sut.TryAcquireAsync(AggregateType, skuA)).Should().BeTrue();
        (await sut.TryAcquireAsync(AggregateType, skuB))
            .Should().BeTrue("a different aggregate id uses a different lock key, so it is unaffected");

        // Even a blocking acquire on a third id returns immediately — they are independent locks.
        var writerC = sut.AcquireAsync(AggregateType, Guid.NewGuid());
        (await Task.WhenAny(writerC, Task.Delay(5000))).Should().Be(writerC, "distinct aggregates never contend");
        await writerC;
    }

    [Fact]
    public async Task The_lock_key_is_namespaced_per_aggregate_type_and_id()
    {
        var fake = new FakeDistributedLock();
        var sut = new DistributedAggregateWriteLock(fake);

        var id = Guid.NewGuid();

        // Same type + id → one stable, namespaced key.
        await sut.AcquireAsync(AggregateType, id);
        fake.RequestedResources.Should().ContainSingle().Which.Should().Be(ResourceKey(id));

        // Same id but a different aggregate type → a different key (no cross-type collisions).
        await sut.AcquireAsync("Warehouse", id);
        fake.RequestedResources[1].Should().Be($"relay:aggregate:Warehouse:{id:N}");
        fake.RequestedResources[1].Should().NotBe(fake.RequestedResources[0], "the key is namespaced by aggregate type as well as id");

        // A different id of the same type → a different key (per-aggregate, not per-type, exclusion).
        var other = Guid.NewGuid();
        await sut.AcquireAsync(AggregateType, other);
        fake.RequestedResources[2].Should().Be(ResourceKey(other));
        fake.RequestedResources[2].Should().NotBe(fake.RequestedResources[0], "each aggregate id gets its own lock");
    }

    [Fact]
    public async Task A_blank_aggregate_type_is_rejected()
    {
        var sut = new DistributedAggregateWriteLock(new FakeDistributedLock());

        Func<Task> act = () => sut.AcquireAsync("", Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>("a lock key must be namespaced by a real aggregate type");
    }
}
