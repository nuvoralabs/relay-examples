using Transport.Sample;

using var provider = TransportScenario.BuildProvider();

var received = await TransportScenario.PublishAndConsumeAsync(provider, "order.placed", "order.shipped");

Console.WriteLine($"Consumer received {received.Count} message(s): {string.Join(", ", received)}");
