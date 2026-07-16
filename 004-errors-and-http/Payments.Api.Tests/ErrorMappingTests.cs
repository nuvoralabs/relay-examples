using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nuvora.Nexus.Relay.Web.Models;
using Payments.Api.Payments;
using Xunit;

namespace Payments.Api.Tests;

/// <summary>
/// One test per exception → HTTP mapping. Each asserts the status code (which is never written in the
/// handlers — the Web mapper derives it from the exception type) and, where relevant, the ProblemDetails
/// body.
/// </summary>
public sealed class ErrorMappingTests
{
    private static object Body(decimal amount, string currency, string card) => new { amount, currency, card };

    [Fact]
    public async Task Authorizing_a_valid_payment_returns_200()
    {
        using var server = PaymentsTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.PostAsJsonAsync("/payments", Body(100m, "usd", "4111 1111 1111 1234"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await response.Content.ReadFromJsonAsync<Payment>();
        payment!.Status.Should().Be("Authorized");
        payment.Currency.Should().Be("USD");          // normalised
        payment.MaskedCard.Should().EndWith("1234");  // never echoes the full PAN
    }

    [Fact]
    public async Task Zero_amount_fails_pipeline_validation_with_400()
    {
        using var server = PaymentsTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.PostAsJsonAsync("/payments", Body(0m, "USD", "4111111111111234"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Status.Should().Be(400);
        // The pipeline validator produces a message but no structured field errors.
        problem.Errors.Should().BeNull();
    }

    [Fact]
    public async Task Unsupported_currency_returns_400_with_field_errors()
    {
        using var server = PaymentsTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.PostAsJsonAsync("/payments", Body(100m, "JPY", "4111111111111234"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        // The handler-thrown Web ValidationException carries a field-keyed Errors dictionary.
        problem!.Errors.Should().ContainKey("currency");
    }

    [Fact]
    public async Task Over_the_limit_returns_422()
    {
        using var server = PaymentsTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.PostAsJsonAsync("/payments", Body(20_000m, "USD", "4111111111111234"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_declined_card_maps_to_the_custom_402()
    {
        using var server = PaymentsTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.PostAsJsonAsync("/payments", Body(100m, "USD", "4111 1111 1111 0000"));

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired); // 402, from CustomExceptionMappings
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Title.Should().Be("Insufficient Funds");
        problem.Type.Should().Be("https://api.payments.example/problems/insufficient-funds");
    }

    [Fact]
    public async Task Capturing_an_unknown_payment_returns_404()
    {
        using var server = PaymentsTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.PostAsync("/payments/pay_missing/capture", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Capturing_twice_returns_409()
    {
        using var server = PaymentsTestHost.Create();
        using var client = server.CreateClient();

        var authorized = await (await client.PostAsJsonAsync("/payments", Body(100m, "USD", "4111111111111234")))
            .Content.ReadFromJsonAsync<Payment>();

        (await client.PostAsync($"/payments/{authorized!.Id}/capture", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsync($"/payments/{authorized.Id}/capture", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Getting_an_unknown_payment_returns_404()
    {
        using var server = PaymentsTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/payments/pay_missing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Error_responses_are_problem_json_with_a_trace_id()
    {
        using var server = PaymentsTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/payments/pay_missing");

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Status.Should().Be(404);
        problem.TraceId.Should().NotBeNullOrEmpty();
        problem.Instance.Should().Be("/payments/pay_missing");
    }
}
