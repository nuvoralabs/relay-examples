using Fulfillment;
using Fulfillment.Tickets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nuvora.Nexus.Relay.Bus;

// A tiny host: build the container, dispatch a few commands through the bus, and observe that the
// read model and the alert log were updated by domain-event handlers — not by the command handlers.

var services = new ServiceCollection();
services.AddLogging(); // the buses depend on ILogger<T>
services.AddFulfillment();

await using var provider = services.BuildServiceProvider();

var commands = provider.GetRequiredService<ICommandBus>();
var queries = provider.GetRequiredService<IQueryBus>();
var ct = CancellationToken.None;

var ticketId = await commands.Execute<OpenTicketCommand, Guid>(new OpenTicketCommand("Cannot log in", "high"), ct);
await commands.Execute<AssignTicketCommand>(new AssignTicketCommand(ticketId, "agent-amy"), ct);
await commands.Execute<CloseTicketCommand>(new CloseTicketCommand(ticketId, "Reset the customer's password"), ct);

var view = await queries.Execute<GetTicketViewQuery, TicketView?>(new GetTicketViewQuery(ticketId), ct);
Console.WriteLine($"Ticket \"{view!.Subject}\" is now {view.Status}, assigned to {view.Assignee}.");

foreach (var alert in provider.GetRequiredService<AlertLog>().All)
{
    Console.WriteLine($"ALERT: {alert}");
}
