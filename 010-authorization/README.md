# Sample 010 — Authorization

Companion to **[Article 010 — Authorization](../../docs/articles/010-authorization.md)**.

A database-free Documents API showing Relay's declarative, attribute-based authorization. Each message
declares *who may run it*; the authorization pipeline behaviors (from `AddRelayAuth`) enforce it and
throw `UnauthorizedException` (401) / `ForbiddenException` (403), which the Web layer maps.

| Message | Attribute | Effect |
|---|---|---|
| `ListDocumentsQuery` | `[AllowAnonymous]` | public |
| `GetDocumentQuery` | `[RequireAuthentication]` | any signed-in user (401 if anonymous) |
| `CreateDocumentCommand` | `[RequireRole("Editor")]` | must hold the role (403 otherwise) |
| `PublishDocumentCommand` | `[RequirePermission("documents:publish")]` | must hold the permission |

Credential validation is **not** Relay's job — a header-based `DevAuthentication` shim stands in for
ASP.NET Core authentication (`X-User-Id` = a GUID, `X-Roles`, `X-Permissions`). In production you swap
it for `AddAuthentication().AddJwtBearer(...)` + `UseAuthentication()`; everything else is unchanged.

## Run it

```bash
dotnet run --project samples/010-authorization/Documents.Api

curl -i localhost:5000/documents                                   # 200 (public)
curl -i localhost:5000/documents/doc-1                             # 401 (auth required)
curl -i localhost:5000/documents/doc-1 -H 'X-User-Id: 11111111-1111-1111-1111-111111111111'  # 200
curl -i -X POST localhost:5000/documents -H 'X-User-Id: 1111...' -H 'X-Roles: Editor' \
  -H 'content-type: application/json' -d '{"title":"Draft","body":"..."}'                     # 200
curl -i -X POST localhost:5000/documents/doc-1/publish -H 'X-User-Id: 1111...' \
  -H 'X-Permissions: documents:publish'                                                       # 200
```

## Test it

```bash
dotnet test samples/010-authorization/Documents.Api.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
