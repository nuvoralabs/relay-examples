using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Cache.InMemory.DependencyInjection;
using Nuvora.Nexus.Relay.Core.Application.Queries;
using Nuvora.Nexus.Relay.Core.Diagnostics;
using Nuvora.Nexus.Relay.Core.Tenancy;
using Xunit;

namespace TelemetrySegmentation.Sample;

// A query that opts a business dimension into Relay's metrics: the [MetricTag("region")] property value
// is added as a tag to relay.queries.executed, relay.query.duration, and the trace span — so a single
// dashboard can break latency/throughput down per region. Keep tagged fields BOUNDED and categorical
// (region, plan tier, channel) — never per-entity ids.
public sealed record GetOrders(Guid TenantId, [property: MetricTag("region")] string Region)
    : IQuery<IReadOnlyList<string>>;

public sealed class GetOrdersHandler : IQueryHandler<GetOrders, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(GetOrders query, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>(["order-1", "order-2"]);
}

/// <summary>
/// Demonstrates Relay's opt-in telemetry <b>segmentation</b> (Option G): a <c>[MetricTag]</c> property is
/// always emitted as a metric/span dimension, while the ambient <c>tenant</c> tag is added only when
/// <c>RelayTelemetryOptions.TagTenant</c> is turned on. Proven with in-process listeners — no exporter or
/// database required, exactly like sample 021.
/// </summary>
public sealed class SegmentationTests
{
    // A tiny ambient-tenant accessor for the sample. In a real service AddRelayTenancy() wires the real one
    // (resolved from the request — header, claim, host); here we pin one so the test is deterministic.
    private sealed class FixedTenant(Guid id) : IRelayTenantAccessor
    {
        public Guid? CurrentTenantId { get; } = id;
        public IDisposable EnterTenant(Guid? tenantId) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }

    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ServiceProvider BuildProvider(bool tagTenant)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRelayTenantAccessor>(new FixedTenant(Tenant));
        services.Configure<RelayTelemetryOptions>(o => o.TagTenant = tagTenant);
        services.AddRelay(typeof(GetOrders).Assembly);
        services.AddRelayInMemoryCache();
        return services.BuildServiceProvider();
    }

    private static (MeterListener Listener, List<Dictionary<string, object?>> Tags) ListenCounter()
    {
        var captured = new List<Dictionary<string, object?>>();
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == RelayTelemetry.MeterName && instrument.Name == "relay.queries.executed")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            var snapshot = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                snapshot[tag.Key] = tag.Value;
            }

            lock (captured)
            {
                captured.Add(snapshot);
            }
        });
        listener.Start();
        return (listener, captured);
    }

    [Fact]
    public async Task MetricTag_segments_the_query_metric_by_region()
    {
        var (listener, tags) = ListenCounter();
        using var _ = listener;

        await using var provider = BuildProvider(tagTenant: false);
        await provider.GetRequiredService<IQueryBus>()
            .Execute<GetOrders, IReadOnlyList<string>>(new GetOrders(Tenant, "eu-west"), CancellationToken.None);

        var measurement = tags.Should().ContainSingle().Subject;
        measurement.Should().Contain(new KeyValuePair<string, object?>("query", nameof(GetOrders)));
        measurement.Should().Contain(new KeyValuePair<string, object?>("region", "eu-west"));
        measurement.Should().Contain(new KeyValuePair<string, object?>("success", true));
        // Tenant tagging is OFF by default — no per-tenant series unless you opt in.
        measurement.Should().NotContainKey("tenant");
    }

    [Fact]
    public async Task Region_dimension_also_lands_on_the_trace_span()
    {
        var spans = new List<Activity>();
        using var spanListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RelayTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (spans) spans.Add(activity); }
        };
        ActivitySource.AddActivityListener(spanListener);

        await using var provider = BuildProvider(tagTenant: false);
        await provider.GetRequiredService<IQueryBus>()
            .Execute<GetOrders, IReadOnlyList<string>>(new GetOrders(Tenant, "ap-south"), CancellationToken.None);

        spans.Should().ContainSingle(s => s.OperationName == $"query {nameof(GetOrders)}")
            .Which.GetTagItem("region").Should().Be("ap-south");
    }

    [Fact]
    public async Task Tenant_tag_appears_only_when_opted_in()
    {
        var (listener, tags) = ListenCounter();
        using var _ = listener;

        await using var provider = BuildProvider(tagTenant: true);
        await provider.GetRequiredService<IQueryBus>()
            .Execute<GetOrders, IReadOnlyList<string>>(new GetOrders(Tenant, "eu-west"), CancellationToken.None);

        tags.Should().ContainSingle().Which.Should()
            .Contain(new KeyValuePair<string, object?>("tenant", Tenant.ToString()));
    }
}
