using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Scheduling.Domain;
using Nuvora.Nexus.Relay.Scheduling.Dispatch;
using Nuvora.Nexus.Relay.Scheduling.Jobs;
using Nuvora.Nexus.Relay.Scheduling.Serialization;
using Xunit;

namespace Recurring.Sample;

public sealed class JobTests
{
    [Fact]
    public async Task Enqueuing_a_job_stages_a_job_kind_message()
    {
        var scheduler = new CapturingScheduler();
        var jobScheduler = new JobScheduler(scheduler, new SystemTextJsonScheduledMessageSerializer());

        await jobScheduler.EnqueueAsync(new CleanupJob { Target = "temp-files" });

        scheduler.Captured.Should().NotBeNull();
        scheduler.Captured!.Kind.Should().Be(ScheduledDeliveryKind.Job);
        scheduler.Captured.MessageType.Should().Contain(nameof(CleanupJob));
    }

    [Fact]
    public async Task A_dispatched_job_invokes_its_registered_handler()
    {
        var serializer = new SystemTextJsonScheduledMessageSerializer();
        var scheduler = new CapturingScheduler();
        var jobScheduler = new JobScheduler(scheduler, serializer);

        var handler = new CleanupJobHandler();
        await using var provider = new ServiceCollection()
            .AddSingleton<IJobHandler<CleanupJob>>(handler)
            .BuildServiceProvider();

        await jobScheduler.EnqueueAsync(new CleanupJob { Target = "temp-files" });

        var dispatcher = new JobScheduledMessageDispatcher(serializer);
        await dispatcher.DispatchAsync(scheduler.Captured!, provider, CancellationToken.None);

        handler.Handled.Should().NotBeNull();
        handler.Handled!.Target.Should().Be("temp-files");
    }
}
