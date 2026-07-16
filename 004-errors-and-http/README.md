# Sample 004 — Errors & HTTP

Companion to **[Article 004 — Errors & HTTP](../../docs/articles/004-errors-and-http.md)**.

A database-free Payments API that exercises Relay's exception → HTTP mapping. Every handler throws
*meaning* (a `NotFoundException`, a `BusinessRuleException`, a custom `InsufficientFundsException`) and
never a status code; `AddRelayExceptionHandling` / `UseRelayExceptionHandling` turn each into an
RFC 7807 `application/problem+json` response.

| Request | Throws | HTTP |
|---|---|---|
| `POST /payments` with `amount: 0` | pipeline `ValidationException` (validator) | **400** (no field errors) |
| `POST /payments` with `currency: "JPY"` | Web `ValidationException` (handler, with errors) | **400** (`errors.currency`) |
| `POST /payments` with `amount: 20000` | `BusinessRuleException` | **422** |
| `POST /payments` with a card ending `0000` | `InsufficientFundsException` (custom mapping) | **402** |
| `POST /payments/{unknown}/capture` | `NotFoundException` | **404** |
| capture an already-captured payment | `ConflictException` | **409** |
| `GET /payments/{unknown}` | `NotFoundException` | **404** |

## Layout

```
Payments.Api/
  Program.cs                              # UseRelayExceptionHandling + MapRelayEndpoints
  PaymentsServiceCollectionExtensions.cs  # AddRelayExceptionHandling + CustomExceptionMappings (402)
  Payments/Payment.cs                     # record + store + InsufficientFundsException
  Payments/Commands.cs                    # authorize/capture + validator + handlers
  Payments/Queries.cs                     # get-by-id (throws NotFound)
Payments.Api.Tests/
  ErrorMappingTests.cs                    # one test per status-code mapping, asserting ProblemDetails
```

## Run it

```bash
dotnet run --project samples/004-errors-and-http/Payments.Api
```

```bash
# 200
curl -i -X POST localhost:5000/payments -H 'content-type: application/json' \
  -d '{"amount":100,"currency":"usd","card":"4111 1111 1111 1234"}'

# 400 with field errors
curl -i -X POST localhost:5000/payments -H 'content-type: application/json' \
  -d '{"amount":100,"currency":"JPY","card":"4111111111111234"}'

# 422 (over limit)
curl -i -X POST localhost:5000/payments -H 'content-type: application/json' \
  -d '{"amount":20000,"currency":"USD","card":"4111111111111234"}'

# 402 (declined; custom mapping)
curl -i -X POST localhost:5000/payments -H 'content-type: application/json' \
  -d '{"amount":100,"currency":"USD","card":"4111 1111 1111 0000"}'

# 404
curl -i localhost:5000/payments/pay_missing
```

Every error body is `application/problem+json` with `type`, `title`, `status`, `detail`, `instance`
and a `traceId` you can correlate with the logs.

## Test it

```bash
dotnet test samples/004-errors-and-http/Payments.Api.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
