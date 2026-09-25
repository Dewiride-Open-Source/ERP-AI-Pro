# HTTP conventions

How every API route behaves on the wire, how its contract is published and how the web app consumes it. The decision and its reasons are in [ADR-0017](../adr/0017-http-conventions-openapi-snapshot-and-generated-client.md); the middleware order, the body limit and the request timeout are in [request pipeline](request-pipeline.md).

## Correlation id

| Rule | Detail |
|---|---|
| Header | `X-Correlation-ID`, on every response of the API |
| Accepted from the caller | one value, at most 64 characters of `[A-Za-z0-9._-]` (`CorrelationId.IsWellFormed`) |
| Otherwise | the W3C trace id of the request activity (32 lowercase hex characters), else a version 7 GUID in `N` format |
| Where it appears | `ICorrelationIdFeature` on the `HttpContext`, the activity tag `erp.correlation_id`, the logger scope `CorrelationId` for the whole request, and `traceId` in every problem body |
| Proved by | `CorrelationIdMiddlewareTests`, `CorrelationIdTests` (host), `api-problems.spec.ts` |

## Problem details

Every non-2xx body is `application/problem+json` (RFC 9457), health endpoints included, and carries:

| Member | Value |
|---|---|
| `type` | `/problems/<code>` |
| `code` | the error's code (`Error.Code` for a failed `Result`) or the default of the status |
| `status` | the HTTP status |
| `title` | a human-readable summary; the generated client copies it into the thrown error's `message` |
| `instance` | the request path |
| `traceId` | the correlation id of the request |
| `errors` | validation problems only: member name (camelCase) → messages |

Default codes, used when nothing more specific was set: `request.invalid` (400), `request.unauthenticated` (401), `request.forbidden` (403), `resource.not-found` (404), `request.method-not-allowed` (405), `request.too-large` (413), `request.unsupported-media-type` (415), `rate-limit.exceeded` (429), `service.unavailable` (503), `request.timeout` (504), `request.rejected` (any other 4xx) and `server.error` (any other 5xx). Codes raised by the platform: `request.host-not-allowed` (a `Host` outside `Erp:Platform:Host:AllowedHosts`), `request.malformed` (a value the binder cannot read, such as `?take=many` or a body that is not JSON; `RouteHandlerOptions.ThrowOnBadRequest` is on in every environment so these always reach `GlobalExceptionHandler`), `feature.disabled` (the module's flag is off), `idempotency.*` and `query.*` (see [application pipeline](application-pipeline.md)). A failing readiness check answers 503 `service.unavailable` and names no check, because `/healthz/ready` is anonymous; a healthy or degraded report stays a plain-text status word.

A replayed idempotent response (`Idempotency-Replayed: true`) carries the stored body with its `traceId` rewritten to the replay's correlation id; the log line `Replayed the stored problem of request <original> as request <replay>` links the two. A module's own codes are `<aggregate>.<reason>` in kebab case, for example `invoice.already-issued`.

## Request records and validation

A route that takes input binds it to a public request record and lets the built-in validation reject it before the handler runs:

```csharp
namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Endpoints.Requests;

public sealed record ListRecentStartupsRequest(
    [property: FromQuery(Name = "take")]
    [property: Description("How many starts to return, between 1 and 100; 20 when the caller does not ask.")]
    [property: Range(ListRecentStartupsQuery.MinimumCount, ListRecentStartupsQuery.MaximumCount)]
    int? Take);
```

- The record is `public sealed` and lives in `<Feature>/Endpoints/Requests/`; the validation generator skips non-public types without a warning.
- Every attribute uses the `property:` target. `FromQuery(Name = …)` gives the camelCase wire name, `Description` gives the text the OpenAPI document shows, the DataAnnotations attributes (or `IValidatableObject`) give the rules, and the document derives `minimum`, `maximum`, `maxLength`, `pattern` and `required` from them.
- Query values the handler treats as optional are nullable (`int?`), and the default is applied in code; a non-nullable value type would make the parameter mandatory.
- The module's `AddServices` calls `builder.Services.AddValidation()`; without that call the module's records are silently not validated. `RequestRecordTests` fails the build in that case.
- The endpoint takes the record with `[AsParameters]` for query strings or as the body parameter, and never re-validates it.

## The OpenAPI document

- Built by `Hosts/Api/OpenApi/OpenApiSetup` with `ErpOpenApiOptions.Configure`: OpenAPI 3.1, title `ERP-AI-Pro API`, version `1.0`, no `servers` entry. Development serves it at `/openapi/erp.json` with the Scalar reference at `/scalar`; Production maps neither.
- Every module route group documents the 400 validation problem and the 404 and 500 problems; an endpoint declares any other status it answers with `ProducesProblem`.
- Operations are described with `.WithName("<Domain>.<Module>.<Action>")` and `.WithSummary(…)`. Response records stay internal to the module, so their XML comments do not reach the document.
- `docs/openapi/erp.json` is the committed contract. After changing an endpoint, refresh it:

  ```
  cd backend && ERP_OPENAPI_SNAPSHOT=update dotnet test --project Tests/Host/Dewiride.Erp.Host.Api.IntegrationTests/Dewiride.Erp.Host.Api.IntegrationTests.csproj --filter-class "*OpenApiSnapshotTests"
  ```

- `OpenApiSchemaShapeTests` rejects composed or conditional schemas (`oneOf`, `anyOf`, `allOf`, `not`, `discriminator`, `patternProperties`, `if`/`then`/`else`) and schema ids that are not PascalCase identifiers, because the TypeScript generator mishandles them. Two response types with the same class name in different modules fail the document build; rename one.

## The generated client

| Piece | Location |
|---|---|
| Generator | Kiota CLI, local tool `kiota` in `backend/.config/dotnet-tools.json` (`dotnet tool restore`) |
| Arguments | `scripts/api-client/lib/kiota.ts` |
| Output (committed) | `frontend/packages/api-client/src/generated`, including `kiota-lock.json` |
| Package | `@dewiride/erp-api-client`: `connect()`, the `ErpApiClient` type and every model type; built to `dist/` by `pnpm build:api-client` |
| Web entry point | `frontend/apps/web/src/shared/api/client.ts` (`apiClient()`, `callApi()`), the only HTTP client of the web app |
| Errors | `frontend/apps/web/src/shared/api/problem-details.ts` (`ApiError`, `toApiError`) |

Regenerate after refreshing the document, or both in one go:

```
node scripts/api-client/generate.ts                 # refreshes docs/openapi/erp.json (needs ERP_TEST_SQL_CONNECTION), then regenerates
node scripts/api-client/generate.ts --skip-snapshot # regenerates from the committed document only
node scripts/api-client/drift.ts                    # the CI check: fails when the committed client differs or generation is not deterministic
```

A query reads through `callApi`, which throws `ApiError` for any problem the API answered and lets a network failure through unchanged:

```ts
export const getRecentStartups = cache(() =>
  callApi((client) => client.api.platform.systemInfo.startups.get({ queryParameters: { take: 20 } })),
);
```

Model members are optional and a JSON `null` arrives as `undefined`; read them with `??`. Date-time members arrive as `Date`.
