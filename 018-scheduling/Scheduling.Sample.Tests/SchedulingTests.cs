using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.Scheduling;
using Nuvora.Nexus.Relay.Scheduling.Dispatch;
using Nuvora.Nexus.Relay.Scheduling.Domain;
using Nuvora.Nexus.Relay.Scheduling.Serialization;
using Xunit;

namespace Scheduling.Sample;

public sealed class SchedulingTests
{
    [Fact]
    public async Task Scheduling_a_command_stages_a_command_message_with_the_right_due_time()
    {
        var repository = new FakeSchedulerRepository();
        var scheduler = new Scheduler(repository, new SystemTextJsonScheduledMessageSerializer());

        await scheduler.ScheduleCommandAsync(new SendReminderCommand("renew subscription"), TimeSpan.FromMinutes(30));

        repository.Scheduled.Should().ContainSingle();
        var message = repository.Scheduled[0];
        message.Kind.Should().Be(ScheduledDeliveryKind.Command);
        message.ScheduledFor.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_due_scheduled_command_is_dispatched_and_executed()
    {
        // The command bus + handler live in a normal DI container.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ReminderLog>();
        services.AddRelay(typeof(SendReminderCommand).Assembly);
        using var provider = services.BuildServiceProvider();

        var serializer = new SystemTextJsonScheduledMessageSerializer();
        var repository = new FakeSchedulerRepository();
        var scheduler = new Scheduler(repository, serializer);

        // Schedule "due now"; the processor claims due rows — here we claim and dispatch by hand.
        await scheduler.ScheduleCommandAsync(new SendReminderCommand("pay invoice"), TimeSpan.Zero);
        var due = await repository.ClaimDueAsync();

        var dispatcher = new CommandScheduledMessageDispatcher(serializer);
        await dispatcher.DispatchAsync(due.Single(), provider, CancellationToken.None);

        provider.GetRequiredService<ReminderLog>().Messages.Should().Contain("pay invoice");
    }
}
