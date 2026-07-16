using Fulfillment;
using Fulfillment.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Core.Domain;
using Xunit;

namespace Fulfillment.Tests;

/// <summary>
/// Drives commands through the real container (ICommandBus) and asserts that the side effects were
/// produced by the domain-event handlers — the read model and alert log are never touched by the
/// command handlers directly, only by the events those commands raised.
/// </summary>
public sealed class TicketFlowTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFulfillment();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Opening_a_high_priority_ticket_projects_a_read_model_and_raises_an_alert()
    {
        using var provider = BuildProvider();
        var commands = provider.GetRequiredService<ICommandBus>();

        var id = await commands.Execute<OpenTicketCommand, Guid>(
            new OpenTicketCommand("Cannot log in", "high"), CancellationToken.None);

        // The read model was built by TicketReadModelProjector reacting to TicketOpened.
        var view = provider.GetRequiredService<TicketReadModelStore>().Get(id);
        view.Should().NotBeNull();
        view!.Status.Should().Be("Open");

        // A different handler (HighPriorityTicketAlerter) reacted to the SAME event.
        provider.GetRequiredService<AlertLog>().All.Should().ContainSingle()
            .Which.Should().Contain("Cannot log in");
    }

    [Fact]
    public async Task A_low_priority_ticket_projects_but_does_not_alert()
    {
        using var provider = BuildProvider();
        var commands = provider.GetRequiredService<ICommandBus>();

        var id = await commands.Execute<OpenTicketCommand, Guid>(
            new OpenTicketCommand("Typo in footer", "low"), CancellationToken.None);

        provider.GetRequiredService<TicketReadModelStore>().Get(id).Should().NotBeNull();
        provider.GetRequiredService<AlertLog>().All.Should().BeEmpty(); // alerter only fires on "high"
    }

    [Fact]
    public async Task The_full_lifecycle_projects_assigned_then_closed()
    {
        using var provider = BuildProvider();
        var commands = provider.GetRequiredService<ICommandBus>();
        var queries = provider.GetRequiredService<IQueryBus>();

        var id = await commands.Execute<OpenTicketCommand, Guid>(
            new OpenTicketCommand("Payment failed", "high"), CancellationToken.None);
        await commands.Execute<AssignTicketCommand>(new AssignTicketCommand(id, "agent-amy"), CancellationToken.None);
        await commands.Execute<CloseTicketCommand>(new CloseTicketCommand(id, "Retried the charge"), CancellationToken.None);

        var view = await queries.Execute<GetTicketViewQuery, TicketView?>(
            new GetTicketViewQuery(id), CancellationToken.None);

        view!.Status.Should().Be("Closed");
        view.Assignee.Should().Be("agent-amy");
    }

    [Fact]
    public async Task Closing_an_unassigned_ticket_is_rejected_by_the_aggregate()
    {
        using var provider = BuildProvider();
        var commands = provider.GetRequiredService<ICommandBus>();

        var id = await commands.Execute<OpenTicketCommand, Guid>(
            new OpenTicketCommand("Cannot log in", "high"), CancellationToken.None);

        // The ticket is Open, not Assigned — the aggregate's invariant rejects Close, and the
        // DomainException propagates back out through the bus.
        var act = () => commands.Execute<CloseTicketCommand>(
            new CloseTicketCommand(id, "premature"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
