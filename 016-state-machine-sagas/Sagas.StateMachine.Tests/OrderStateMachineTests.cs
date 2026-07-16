using System.Text.Json;
using FluentAssertions;
using Nuvora.Nexus.Relay.Sagas;
using Nuvora.Nexus.Relay.Scheduling.Domain;
using Xunit;

namespace Sagas.StateMachine;

public sealed class OrderStateMachineTests
{
    [Fact]
    public async Task The_initial_transition_starts_the_saga_in_the_submitted_state_and_schedules_a_timeout()
    {
        var harness = SagaHarness.For<OrderStateMachine, OrderState>();
        var orderId = Guid.NewGuid();

        await harness.Coordinator.DeliverAsync(typeof(OrderStateMachine), new OrderPlaced { OrderId = orderId }, default);

        var saga = harness.Repo.Single();
        saga.Status.Should().Be(SagaStatus.Active);
        saga.CurrentState.Should().Be("Submitted");
        harness.Scheduler.Scheduled.Should().ContainSingle(m =>
            m.Kind == ScheduledDeliveryKind.SagaTimeout && m.CorrelationId == saga.SagaId);
    }

    [Fact]
    public async Task Payment_during_submitted_updates_state_cancels_the_timeout_and_finalizes()
    {
        var harness = SagaHarness.For<OrderStateMachine, OrderState>();
        var orderId = Guid.NewGuid();
        await harness.Coordinator.DeliverAsync(typeof(OrderStateMachine), new OrderPlaced { OrderId = orderId }, default);
        var sagaId = harness.Repo.Single().SagaId;

        await harness.Coordinator.DeliverAsync(typeof(OrderStateMachine), new PaymentReceived { OrderId = orderId }, default);

        var saga = harness.Repo.Single();
        saga.Status.Should().Be(SagaStatus.Completed);
        saga.State.Should().Contain("\"Paid\":true");
        harness.Scheduler.CancelledCorrelations.Should().Contain(sagaId);
    }

    [Fact]
    public async Task A_message_with_no_transition_in_the_current_state_is_ignored()
    {
        var harness = SagaHarness.For<OrderStateMachine, OrderState>();

        // PaymentReceived is only valid During(Submitted); with no live saga it is not a start trigger.
        await harness.Coordinator.DeliverAsync(typeof(OrderStateMachine), new PaymentReceived { OrderId = Guid.NewGuid() }, default);

        harness.Repo.All.Should().BeEmpty();
    }

    [Fact]
    public async Task The_expiry_timeout_finalizes_the_saga()
    {
        var harness = SagaHarness.For<OrderStateMachine, OrderState>();
        var orderId = Guid.NewGuid();
        await harness.Coordinator.DeliverAsync(typeof(OrderStateMachine), new OrderPlaced { OrderId = orderId }, default);
        var saga = harness.Repo.Single();

        await harness.Coordinator.DeliverTimeoutAsync(
            typeof(OrderStateMachine).FullName!, saga.SagaId, typeof(OrderExpired).FullName!,
            JsonSerializer.Serialize(new OrderExpired { OrderId = orderId }), default);

        harness.Repo.Single().Status.Should().Be(SagaStatus.Completed);
    }
}
