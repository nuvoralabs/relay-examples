using System.Text.Json;
using FluentAssertions;
using Nuvora.Nexus.Relay.Sagas;
using Nuvora.Nexus.Relay.Scheduling.Domain;
using Xunit;

namespace Sagas.Sample;

public sealed class OrderSagaTests
{
    [Fact]
    public async Task A_start_trigger_creates_an_active_saga_and_schedules_its_timeout()
    {
        var harness = SagaHarness.For<OrderSaga, OrderState>();
        var orderId = Guid.NewGuid();

        await harness.Coordinator.DeliverAsync(typeof(OrderSaga), new OrderPlaced { OrderId = orderId }, default);

        var saga = harness.Repo.Single();
        saga.Status.Should().Be(SagaStatus.Active);
        saga.State.Should().Contain(orderId.ToString());
        harness.Scheduler.Scheduled.Should().ContainSingle(m =>
            m.Kind == ScheduledDeliveryKind.SagaTimeout && m.CorrelationId == saga.SagaId);
    }

    [Fact]
    public async Task A_correlated_payment_completes_the_saga_and_cancels_the_timeout()
    {
        var harness = SagaHarness.For<OrderSaga, OrderState>();
        var orderId = Guid.NewGuid();
        await harness.Coordinator.DeliverAsync(typeof(OrderSaga), new OrderPlaced { OrderId = orderId }, default);
        var sagaId = harness.Repo.Single().SagaId;

        await harness.Coordinator.DeliverAsync(typeof(OrderSaga), new PaymentReceived { OrderId = orderId }, default);

        var saga = harness.Repo.Single();
        saga.Status.Should().Be(SagaStatus.Completed);
        saga.State.Should().Contain("\"Paid\":true");
        harness.Scheduler.CancelledCorrelations.Should().Contain(sagaId);
    }

    [Fact]
    public async Task A_non_start_message_for_no_existing_saga_is_ignored()
    {
        var harness = SagaHarness.For<OrderSaga, OrderState>();

        await harness.Coordinator.DeliverAsync(typeof(OrderSaga), new PaymentReceived { OrderId = Guid.NewGuid() }, default);

        harness.Repo.All.Should().BeEmpty();
        harness.Scheduler.Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task A_fired_timeout_drives_the_saga_to_completion()
    {
        var harness = SagaHarness.For<OrderSaga, OrderState>();
        var orderId = Guid.NewGuid();
        await harness.Coordinator.DeliverAsync(typeof(OrderSaga), new OrderPlaced { OrderId = orderId }, default);
        var saga = harness.Repo.Single();

        await harness.Coordinator.DeliverTimeoutAsync(
            typeof(OrderSaga).FullName!, saga.SagaId, typeof(OrderExpired).FullName!,
            JsonSerializer.Serialize(new OrderExpired { OrderId = orderId }), default);

        harness.Repo.Single().Status.Should().Be(SagaStatus.Completed);
    }
}
