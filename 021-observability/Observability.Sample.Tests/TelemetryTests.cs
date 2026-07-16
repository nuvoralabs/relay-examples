using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Cache.InMemory.DependencyInjection;
using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Core.Diagnostics;
using Xunit;

namespace Observability.Sample;

// A trivial command so we can watch Relay emit telemetry as the bus dispatches it.
[SkipTransaction]
public sealed record PingCommand(string Message) : ICommand<string>;

public sealed class PingCommandHandler : ICommandHandler<PingCommand, string>
{
    public Task<string> Handle(PingCommand command, CancellationToken cancellationToken)
        => Task.FromResult($"pong:{command.Message}");
}

/// <summary>
/// Relay publishes OpenTelemetry-native signals from one <see cref="ActivitySource"/> and one
/// <see cref="Meter"/>, both named <c>Nuvora.Nexus.Relay</c>. In production you forward them with
/// <c>AddRelayInstrumentation()</c> on a TracerProvider/MeterProvider; here we attach in-process
/// listeners — the same mechanism, no exporter required — to prove the signals are emitted.
/// </summary>
public sealed class TelemetryTests
{
    [Fact]
    public void The_relay_instruments_have_stable_published_names()
    {
        // These names are the public contract your dashboards and alerts bind to.
        RelayTelemetry.SourceName.Should().Be("Nuvora.Nexus.Relay");
        RelayTelemetry.MeterName.Should().Be("Nuvora.Nexus.Relay");
        RelayTelemetry.CommandsExecuted.Name.Should().Be("relay.commands.executed");
        RelayTelemetry.CommandDuration.Name.Should().Be("relay.command.duration");
        RelayTelemetry.CommandDuration.Unit.Should().Be("ms");
    }

    [Fact]
    public async Task Executing_a_command_records_a_metric_and_starts_a_trace_span()
    {
        // 1) Capture the "relay.commands.executed" counter via a MeterListener.
        var metrics = new List<(long Value, string? Command, bool? Success)>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == RelayTelemetry.MeterName && instrument.Name == "relay.commands.executed")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string? command = null;
            bool? success = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "command") command = tag.Value as string;
                if (tag.Key == "success") success = tag.Value as bool?;
            }
            lock (metrics)
            {
                metrics.Add((value, command, success));
            }
        });
        meterListener.Start();

        // 2) Capture spans from Relay's ActivitySource.
        var spans = new List<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RelayTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (spans) spans.Add(activity); },
        };
        ActivitySource.AddActivityListener(activityListener);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRelay(typeof(PingCommand).Assembly);
        services.AddRelayInMemoryCache();
        await using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<ICommandBus>()
            .Execute<PingCommand, string>(new PingCommand("hello"), CancellationToken.None);

        result.Should().Be("pong:hello");
        metrics.Should().ContainSingle(m => m.Value == 1 && m.Command == nameof(PingCommand) && m.Success == true);
        spans.Should().Contain(s => s.OperationName == $"command {nameof(PingCommand)}");
    }
}
