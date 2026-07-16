using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Core.Coordination;
using Xunit;

namespace Coordination.Sample;

/// <summary>
/// The Postgres-backed <see cref="IDistributedLock"/> (a <c>pg_advisory_lock</c>) is mutual exclusion
/// across processes, not just threads — the database is the arbiter. A session lock is held by a
/// dedicated connection and released on dispose (or when the connection drops). Requires Docker.
/// </summary>
[Collection("coordination")]
public sealed class DistributedLockTests
{
    private readonly CoordinationFixture _fixture;

    public DistributedLockTests(CoordinationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_session_lock_is_exclusive_across_acquirers()
    {
        using var scope = _fixture.Services.CreateScope();
        var locks = scope.ServiceProvider.GetRequiredService<IDistributedLock>();
        var resource = "lock-" + Guid.NewGuid();

        await using var first = await locks.TryAcquireAsync(resource);
        first.Should().NotBeNull();

        var second = await locks.TryAcquireAsync(resource);
        second.Should().BeNull("the resource is already held on another connection");
    }

    [Fact]
    public async Task A_session_lock_is_reacquirable_after_release()
    {
        using var scope = _fixture.Services.CreateScope();
        var locks = scope.ServiceProvider.GetRequiredService<IDistributedLock>();
        var resource = "lock-" + Guid.NewGuid();

        var first = await locks.TryAcquireAsync(resource);
        first.Should().NotBeNull();
        await first!.DisposeAsync();

        await using var second = await locks.TryAcquireAsync(resource);
        second.Should().NotBeNull("the lock was released and can be taken again");
    }

    [Fact]
    public async Task Distinct_resources_do_not_contend()
    {
        using var scope = _fixture.Services.CreateScope();
        var locks = scope.ServiceProvider.GetRequiredService<IDistributedLock>();

        await using var a = await locks.TryAcquireAsync("a-" + Guid.NewGuid());
        await using var b = await locks.TryAcquireAsync("b-" + Guid.NewGuid());

        a.Should().NotBeNull();
        b.Should().NotBeNull("different resources use different advisory-lock keys");
    }
}
