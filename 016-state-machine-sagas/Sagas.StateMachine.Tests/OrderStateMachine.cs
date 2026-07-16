using Nuvora.Nexus.Relay.Core.Application.Events;
using Nuvora.Nexus.Relay.Sagas;
using Nuvora.Nexus.Relay.Sagas.StateMachine;

namespace Sagas.StateMachine;

public sealed record OrderPlaced : IntegrationEvent { public Guid OrderId { get; init; } }
public sealed record PaymentReceived : IntegrationEvent { public Guid OrderId { get; init; } }
public sealed class OrderExpired { public Guid OrderId { get; set; } }

public sealed class OrderState : ISagaState
{
    public Guid OrderId { get; set; }
    public bool Paid { get; set; }
}

/// <summary>
/// The SAME order saga as article 015, authored declaratively as a state machine. You name the states,
/// the correlated events, and the timeouts, then describe the transitions: <c>Initially</c> for the start
/// trigger and <c>During(state, ...)</c> for each state. Each <c>When(...)</c> chains activities —
/// <c>Then</c> (mutate state), <c>Schedule</c> (a timeout), <c>CancelTimeouts</c>, <c>TransitionTo</c>,
/// <c>Finalize</c>. The framework's tests prove this is behaviourally identical to the imperative form;
/// the DSL just makes the state graph explicit and reviewable.
/// </summary>
public sealed class OrderStateMachine : StateMachineSaga<OrderState>
{
    public MachineState Submitted { get; } = new(nameof(Submitted));

    public Event<OrderPlaced> Placed { get; } = new(e => e.OrderId);
    public Event<PaymentReceived> Paid { get; } = new(e => e.OrderId);
    public Timeout<OrderExpired> Expiry { get; } = new();

    protected override void DefineStateMachine()
    {
        Initially(
            When(Placed)
                .Then(e => State.OrderId = e.OrderId)
                .Schedule(Expiry, TimeSpan.FromHours(1), e => new OrderExpired { OrderId = e.OrderId })
                .TransitionTo(Submitted));

        During(Submitted,
            When(Paid)
                .Then(e => State.Paid = true)
                .CancelTimeouts()
                .Finalize(),
            When(Expiry)
                .Finalize());
    }
}
