using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Scheduling;
using Nuvora.Nexus.Relay.Scheduling.Domain;
using Nuvora.Nexus.Relay.Scheduling.Jobs;

namespace Recurring.Sample;

/// <summary>A serialisable unit of background work.</summary>
public sealed class CleanupJob : IJob
{
    public string Target { get; set; } = string.Empty;
}

public sealed class CleanupJobHandler : IJobHandler<CleanupJob>
{
    public CleanupJob? Handled { get; private set; }

    public Task HandleAsync(CleanupJob job, CancellationToken cancellationToken)
    {
        Handled = job;
        return Task.CompletedTask;
    }
}

/// <summary>Captures the scheduled message a <see cref="JobScheduler"/> stages, so tests can inspect/dispatch it.</summary>
internal sealed class CapturingScheduler : IScheduler
{
    public ScheduledMessage? Captured { get; private set; }

    public Task<Guid> ScheduleAsync(ScheduledMessage message, CancellationToken ct = default)
    {
        Captured = message;
        return Task.FromResult(message.Id);
    }

    public Task<Guid> ScheduleCommandAsync<TCommand>(TCommand command, DateTimeOffset fireAt, CancellationToken ct = default) where TCommand : ICommand
        => throw new NotImplementedException();
    public Task<Guid> ScheduleCommandAsync<TCommand>(TCommand command, TimeSpan delay, CancellationToken ct = default) where TCommand : ICommand
        => throw new NotImplementedException();
    public Task<int> CancelAsync(Guid id, CancellationToken ct = default) => Task.FromResult(0);
    public Task<int> CancelByCorrelationAsync(Guid correlationId, CancellationToken ct = default) => Task.FromResult(0);
    public Task<int> CancelByKeyAsync(string cancellationKey, CancellationToken ct = default) => Task.FromResult(0);
}
