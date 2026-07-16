using System.Net;
using System.Net.Http.Json;
using Catalog.Api.Catalog;
using FluentAssertions;
using Nuvora.Nexus.Relay.Web.Models;
using Xunit;

namespace Catalog.Api.Tests;

/// <summary>
/// End-to-end tests that drive the generated HTTP endpoints through an in-process server. They prove
/// the full request path: binding → pipeline (validation) → handler → result/ProblemDetails mapping.
/// </summary>
public sealed class CatalogEndpointTests
{
    [Fact]
    public async Task List_returns_only_active_products_by_default()
    {
        using var server = CatalogTestHost.Create();
        using var client = server.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResult<Product>>("/products");

        page.Should().NotBeNull();
        page!.Items.Should().OnlyContain(p => !p.Discontinued);
        page.Pagination.TotalCount.Should().Be(3); // p-2001 is discontinued and excluded
    }

    [Fact]
    public async Task List_can_include_discontinued_products()
    {
        using var server = CatalogTestHost.Create();
        using var client = server.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResult<Product>>("/products?includeDiscontinued=true");

        page!.Pagination.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task Get_returns_a_known_product()
    {
        using var server = CatalogTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/products/p-1000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await response.Content.ReadFromJsonAsync<Product>();
        product!.Name.Should().Be("Aeron Chair");
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_product()
    {
        using var server = CatalogTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/products/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_persists_a_product_and_makes_it_retrievable()
    {
        using var server = CatalogTestHost.Create();
        using var client = server.CreateClient();

        var create = await client.PostAsJsonAsync("/products", new
        {
            name = "Laptop Stand",
            category = "Peripherals",
            price = 59.95m,
        });

        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<Product>();
        created!.Id.Should().NotBeNullOrEmpty();
        created.Category.Should().Be("peripherals"); // handler normalises to lower case

        var fetched = await client.GetFromJsonAsync<Product>($"/products/{created.Id}");
        fetched!.Name.Should().Be("Laptop Stand");
    }

    [Fact]
    public async Task Create_with_invalid_body_returns_400()
    {
        using var server = CatalogTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.PostAsJsonAsync("/products", new
        {
            name = "",     // required
            category = "", // required
            price = 0m,    // must be > 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_price_changes_the_stored_value()
    {
        using var server = CatalogTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.PutAsJsonAsync("/products/p-2000/price", new { newPrice = 129.99m });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<Product>();
        updated!.Price.Should().Be(129.99m);
    }

    [Fact]
    public async Task Discontinue_marks_the_product_and_hides_it_from_the_default_list()
    {
        using var server = CatalogTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.PostAsync("/products/p-1001/discontinue", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await client.GetFromJsonAsync<PagedResult<Product>>("/products");
        page!.Items.Should().NotContain(p => p.Id == "p-1001");
    }
}
