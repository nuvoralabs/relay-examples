using Catalog.Api.Catalog;
using FluentAssertions;
using Nuvora.Nexus.Relay.Web.Exceptions;
using Xunit;

namespace Catalog.Api.Tests;

/// <summary>
/// Unit tests for the handlers and validator in isolation — no host, no HTTP. A handler is just a
/// class with dependencies, so testing it is as cheap as testing any other class.
/// </summary>
public sealed class CatalogHandlerTests
{
    [Fact]
    public async Task CreateProduct_stores_a_normalised_product()
    {
        var catalog = new ProductCatalog();
        var handler = new CreateProductCommandHandler(catalog);

        var product = await handler.Handle(
            new CreateProductCommand("  Webcam  ", "Peripherals", 89.00m),
            CancellationToken.None);

        product.Name.Should().Be("Webcam");           // trimmed
        product.Category.Should().Be("peripherals");   // lower-cased
        product.Discontinued.Should().BeFalse();
        catalog.Get(product.Id).Should().Be(product);
    }

    [Fact]
    public async Task DiscontinueProduct_throws_NotFound_for_a_missing_id()
    {
        var handler = new DiscontinueProductCommandHandler(new ProductCatalog());

        var act = () => handler.Handle(new DiscontinueProductCommand("nope"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("", "hardware", 10, 1)]   // missing name
    [InlineData("Mouse", "", 10, 1)]      // missing category
    [InlineData("Mouse", "hardware", 0, 1)] // non-positive price
    [InlineData("", "", 0, 3)]            // everything wrong → three failures
    public async Task Validator_reports_failures_for_invalid_commands(
        string name, string category, int price, int expectedFailures)
    {
        var validator = new CreateProductCommandValidator();

        // `price` is an int from InlineData (decimal isn't a legal attribute argument); it widens to
        // the command's decimal Price implicitly.
        var result = await validator.ValidateAsync(
            new CreateProductCommand(name, category, price),
            CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(expectedFailures);
    }

    [Fact]
    public async Task Validator_passes_a_valid_command()
    {
        var validator = new CreateProductCommandValidator();

        var result = await validator.ValidateAsync(
            new CreateProductCommand("Keyboard", "peripherals", 99.99m),
            CancellationToken.None);

        result.IsValid.Should().BeTrue();
    }
}
