using System.Collections.Concurrent;

namespace Catalog.Api.Catalog;

/// <summary>
/// The read/write shape returned by the catalog endpoints. In this first sample the "domain" is a
/// plain record and the "database" is an in-memory dictionary — article 002 introduces a real
/// aggregate, and article 005 a real event-sourced store. Here we focus purely on the CQRS plumbing.
/// </summary>
public sealed record Product(string Id, string Name, string Category, decimal Price, bool Discontinued);

/// <summary>
/// A tiny thread-safe in-memory store, registered as a singleton. It stands in for a repository so
/// the sample needs no database. Because there is no transaction, the commands that mutate it are
/// marked <c>[SkipTransaction]</c> (see Commands.cs).
/// </summary>
public sealed class ProductCatalog
{
    private readonly ConcurrentDictionary<string, Product> _products = new();

    public ProductCatalog()
    {
        Seed(new Product("p-1000", "Aeron Chair", "furniture", 1395.00m, Discontinued: false));
        Seed(new Product("p-1001", "Standing Desk", "furniture", 720.00m, Discontinued: false));
        Seed(new Product("p-2000", "Mechanical Keyboard", "peripherals", 149.99m, Discontinued: false));
        Seed(new Product("p-2001", "Trackball Mouse", "peripherals", 79.99m, Discontinued: true));
    }

    public Product? Get(string id) => _products.GetValueOrDefault(id);

    public IReadOnlyList<Product> List(string? category, bool includeDiscontinued) => _products.Values
        .Where(p => category is null || string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase))
        .Where(p => includeDiscontinued || !p.Discontinued)
        .OrderBy(p => p.Id, StringComparer.Ordinal)
        .ToList();

    public Product Save(Product product)
    {
        _products[product.Id] = product;
        return product;
    }

    private void Seed(Product product) => _products[product.Id] = product;
}
