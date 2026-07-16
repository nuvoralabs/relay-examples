using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Core.Application.Events;
using Nuvora.Nexus.Relay.Sagas;
using Nuvora.Nexus.Relay.Sagas.Infrastructure;
using Nuvora.Nexus.Relay.Sagas.Runtime;
using Nuvora.Nexus.Relay.Sagas.Tenancy;
using Nuvora.Nexus.Relay.Scheduling;
using Nuvora.Nexus.Relay.Scheduling.Domain;
using Nuvora.Nexus.Relay.Scheduling.Serialization;

namespace Sagas.StateMachine;

/// <summary>In-memory saga harness — drives the real coordinator with fakes (no database). See article 015.</summary>
internal sealed class SagaHarness
{
    public FakeSagaRepository Repo { get; } = new();
    public FakeScheduler Scheduler { get; } = new();
    public SagaCoordinator Coordinator { get; }

    private SagaHarness(ISagaRegistry registry)
        => Coordinator = new SagaCoordinator(
            registry, Repo, Scheduler, new FakeIntegrationBus(),
            new SystemTextJsonScheduledMessageSerializer(), new NullSagaTenantProvider(),
            Options.Create(new SagaOptions()), TimeProvider.System, NullLogger<SagaCoordinator>.Instance);

    public static SagaHarness For<TSaga, TState>()
        where TSaga : Saga<TState>, new()
        where TState : class, ISagaState, new()
        => new(new SagaRegistry(new ISagaInvoker[] { new SagaInvoker<TSaga, TState>() }));
}

internal sealed class FakeSagaRepository : ISagaRepository
{
    private readonly List<SagaRecord> _store = new();
    public IReadOnlyList<SagaRecord> All => _store;
    public SagaRecord Single() => _store.Single();

    public Task<SagaRecord?> FindAsync(string sagaType, string correlationKey, Guid? tenantId, CancellationToken ct = default)
        => Task.FromResult(_store.FirstOrDefault(s => s.SagaType == sagaType && s.CorrelationKey == correlationKey && s.TenantId == tenantId));
    public Task<SagaRecord?> FindByIdAsync(string sagaType, Guid sagaId, CancellationToken ct = default)
        => Task.FromResult(_store.FirstOrDefault(s => s.SagaType == sagaType && s.SagaId == sagaId));
    public void Add(SagaRecord saga) => _store.Add(saga);
    public void Update(SagaRecord saga) { }
}

internal sealed class FakeScheduler : IScheduler
{
    public List<ScheduledMessage> Scheduled { get; } = new();
    public List<Guid> CancelledCorrelations { get; } = new();

    public Task<Guid> ScheduleCommandAsync<TCommand>(TCommand command, DateTimeOffset fireAt, CancellationToken ct = default) where TCommand : ICommand
        => Task.FromResult(Guid.NewGuid());
    public Task<Guid> ScheduleCommandAsync<TCommand>(TCommand command, TimeSpan delay, CancellationToken ct = default) where TCommand : ICommand
        => Task.FromResult(Guid.NewGuid());
    public Task<Guid> ScheduleAsync(ScheduledMessage message, CancellationToken ct = default)
    {
        Scheduled.Add(message);
        return Task.FromResult(message.Id);
    }
    public Task<int> CancelAsync(Guid id, CancellationToken ct = default) => Task.FromResult(0);
    public Task<int> CancelByCorrelationAsync(Guid correlationId, CancellationToken ct = default)
    {
        CancelledCorrelations.Add(correlationId);
        return Task.FromResult(1);
    }
    public Task<int> CancelByKeyAsync(string cancellationKey, CancellationToken ct = default) => Task.FromResult(0);
}

internal sealed class FakeIntegrationBus : IIntegrationEventBus
{
    public Task Publish<TEvent>(TEvent integrationEvent, CancellationToken ct) where TEvent : IIntegrationEvent => Task.CompletedTask;
    public Task PublishMany<TEvent>(IEnumerable<TEvent> integrationEvents, CancellationToken ct) where TEvent : IIntegrationEvent => Task.CompletedTask;
    public Task DispatchToLocalHandlers(IIntegrationEvent integrationEvent, CancellationToken ct) => Task.CompletedTask;
}
