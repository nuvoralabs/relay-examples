using Nuvora.Nexus.Relay.Auth.Attributes;
using Nuvora.Nexus.Relay.Core.Application.Queries;
using Nuvora.Nexus.Relay.Http.Attributes;
using Nuvora.Nexus.Relay.Web.Models;

namespace Catalog.Api.Catalog;

// ── Queries ──────────────────────────────────────────────────────────────────────────────────────
//
// Queries read state and never change it. They are records implementing IQuery<TResult>. A query
// returning a reference type that the handler resolves to null is mapped to HTTP 404 by the
// endpoint layer, which is why GetProductQuery can return Product? and skip an explicit not-found
// branch. [AllowAnonymous] declares each query public — Relay is fail-closed and will not start
// otherwise (see Commands.cs / article 010).

/// <summary>Fetch one product by id. Returns <c>null</c> (→ HTTP 404) when it does not exist.</summary>
[RelayHttpGet("/products/{id}")]
[RelayHttpTag("Catalog")]
[AllowAnonymous]
public sealed record GetProductQuery(string Id) : IQuery<Product?>;

/// <summary>
/// List products with optional filtering and pagination. The <c>[RelayPagination]</c> attribute
/// binds the <c>page</c> and <c>pageSize</c> query-string values to <see cref="Page"/> and
/// <see cref="PageSize"/>; the remaining properties bind from the query string because this is a GET.
/// </summary>
[RelayHttpGet("/products")]
[RelayHttpTag("Catalog")]
[RelayPagination]
[AllowAnonymous]
public sealed record ListProductsQuery : IQuery<PagedResult<Product>>
{
    public string? Category { get; init; }
    public bool IncludeDiscontinued { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

// ── Handlers ─────────────────────────────────────────────────────────────────────────────────────

public sealed class GetProductQueryHandler(ProductCatalog catalog) : IQueryHandler<GetProductQuery, Product?>
{
    public Task<Product?> Handle(GetProductQuery query, CancellationToken cancellationToken)
        => Task.FromResult(catalog.Get(query.Id));
}

public sealed class ListProductsQueryHandler(ProductCatalog catalog)
    : IQueryHandler<ListProductsQuery, PagedResult<Product>>
{
    public Task<PagedResult<Product>> Handle(ListProductsQuery query, CancellationToken cancellationToken)
    {
        var all = catalog.List(query.Category, query.IncludeDiscontinued);

        var page = query.Page is > 0 ? query.Page.Value : 1;
        var pageSize = query.PageSize is > 0 ? query.PageSize.Value : PaginationParameters.DefaultPageSize;

        var items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<Product>
        {
            Items = items,
            Pagination = new Pagination
            {
                TotalCount = all.Count,
                Page = page,
                PageSize = pageSize,
            },
        });
    }
}
