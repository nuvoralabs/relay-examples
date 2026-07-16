# Sample 001 — Getting Started (Catalog API)

Companion to **[Article 001 — Getting Started](../../docs/articles/001-getting-started.md)**.

A database-free product catalog that demonstrates the CQRS core of Relay:

- **Commands** that change state (`CreateProductCommand`, `UpdateProductPriceCommand`,
  `DiscontinueProductCommand`) and **queries** that read it (`GetProductQuery`, `ListProductsQuery`).
- **Handlers** auto-discovered by `AddRelay` — never registered by hand.
- A **validator** (`CreateProductCommandValidator`) that runs automatically in the pipeline and turns
  failures into an HTTP 400 `ProblemDetails`.
- **Attribute-routed HTTP endpoints** (`[RelayHttpGet/Post/Put]`) mapped by `MapRelayEndpoints()`,
  with pagination (`[RelayPagination]`).
- **Exception → HTTP mapping** via `AddRelayExceptionHandling` / `UseRelayExceptionHandling`.
- **`[AllowAnonymous]`** on every message. Relay is *fail-closed*: it refuses to start unless each
  command/query declares an authorization posture. This API is public, so each endpoint says so
  explicitly — [article 010](../../docs/articles/010-authorization.md) shows real authorization.

There is no database: state lives in a singleton `ProductCatalog`, so the commands are marked
`[SkipTransaction]`.

## Layout

```
Catalog.Api/
  Program.cs                          # builds the host, maps the endpoints
  CatalogServiceCollectionExtensions  # AddCatalogServices(): AddRelay + exception handling
  Catalog/Product.cs                  # the record + in-memory ProductCatalog store
  Catalog/Commands.cs                 # commands, the validator, command handlers
  Catalog/Queries.cs                  # queries + query handlers
Catalog.Api.Tests/
  CatalogEndpointTests.cs             # in-process TestServer hitting the real endpoints
  CatalogHandlerTests.cs             # unit tests for handlers + validator
```

## Run it

```bash
dotnet run --project samples/001-getting-started/Catalog.Api
```

Then (substitute the port the host prints):

```bash
# list active products (paginated)
curl http://localhost:5000/products

# include discontinued ones
curl "http://localhost:5000/products?includeDiscontinued=true&pageSize=2"

# fetch one (404 if unknown)
curl -i http://localhost:5000/products/p-1000

# create one (returns the created product)
curl -X POST http://localhost:5000/products \
  -H 'content-type: application/json' \
  -d '{"name":"Laptop Stand","category":"Peripherals","price":59.95}'

# a validation failure → HTTP 400 ProblemDetails
curl -i -X POST http://localhost:5000/products \
  -H 'content-type: application/json' \
  -d '{"name":"","category":"","price":0}'

# change a price / discontinue
curl -X PUT  http://localhost:5000/products/p-2000/price -H 'content-type: application/json' -d '{"newPrice":129.99}'
curl -X POST http://localhost:5000/products/p-1001/discontinue
```

## Test it

```bash
dotnet test samples/001-getting-started/Catalog.Api.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
