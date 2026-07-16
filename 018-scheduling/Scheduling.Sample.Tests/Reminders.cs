using System.Collections.Concurrent;
using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Scheduling.Domain;
using Nuvora.Nexus.Relay.Scheduling.Infrastructure;

namespace Scheduling.Sample;

// A command we want to run LATER (a reminder). [SkipTransaction] because this sample has no database.
[SkipTransaction]
public sealed record SendReminderCommand(string Message) : ICommand;

public sealed class SendReminderCommandHandler(ReminderLog log) : ICommandHandler<SendReminderCommand>
{
    public Task Handle(SendReminderCommand command, CancellationToken cancellationToken)
    {
        log.Add(command.Message);
        return Task.CompletedTask;
    }
}

public sealed class ReminderLog
{
    private readonly ConcurrentQueue<string> _messages = new();
    public void Add(string message) => _messages.Enqueue(message);
    public IReadOnlyList<string> Messages => _messages.ToArray();
}

/// <summary>
/// An in-memory <see cref="ISchedulerRepository"/> so the sample needs no database. In production the EF
/// Core repository (<c>AddRelaySchedulerEfCore</c>) stores scheduled messages in PostgreSQL and the
/// <c>SchedulerProcessor</c> claims due rows with <c>FOR UPDATE SKIP LOCKED</c>.
/// </summary>
internal sealed class FakeSchedulerRepository : ISchedulerRepository
{
    public List<ScheduledMessage> Scheduled { get; } = new();

    public Task ScheduleAsync(ScheduledMessage message, CancellationToken ct = default)
    {
        Scheduled.Add(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScheduledMessage>> ClaimDueAsync(int batchSize = 100, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ScheduledMessage>>(
            Scheduled.Where(m => m.ScheduledFor <= DateTimeOffset.UtcNow).Take(batchSize).ToList());

    public Task MarkExecutedAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    public Task<ScheduledFailureOutcome> RecordFailureAsync(Guid id, string errorMessage, int maxRetries, CancellationToken ct = default)
        => Task.FromResult(ScheduledFailureOutcome.ScheduledForRetry);
    public Task<int> CancelAsync(Guid id, CancellationToken ct = default) => Task.FromResult(0);
    public Task<int> CancelByCorrelationAsync(Guid correlationId, CancellationToken ct = default) => Task.FromResult(0);
    public Task<int> CancelByKeyAsync(string cancellationKey, CancellationToken ct = default) => Task.FromResult(0);
    public Task<DateTimeOffset?> GetNextDueAtAsync(CancellationToken ct = default) => Task.FromResult<DateTimeOffset?>(null);
    public Task<SchedulerBacklog> GetBacklogAsync(CancellationToken ct = default)
        => Task.FromResult(new SchedulerBacklog(0, Scheduled.Count, 0, null));
    public Task<IReadOnlyList<ScheduledMessage>> GetFailedAsync(int batchSize = 100, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ScheduledMessage>>(new List<ScheduledMessage>());
    public Task<IReadOnlyList<ScheduledMessage>> GetUpcomingAsync(int batchSize = 100, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ScheduledMessage>>(Scheduled.Take(batchSize).ToList());
    public Task<bool> RequeueFailedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> DiscardFailedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
    public Task<int> DeleteCompletedAsync(DateTimeOffset olderThan, CancellationToken ct = default) => Task.FromResult(0);
}
