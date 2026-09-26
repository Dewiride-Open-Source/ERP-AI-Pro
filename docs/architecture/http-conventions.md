# HTTP conventions

How every API route behaves on the wire, how its contract is published and how the web app consumes it. The decision and its reasons are in [ADR-0017](../adr/0017-http-conventions-openapi-snapshot-and-generated-client.md), extended for enums, paging parameters and files by [ADR-0022](../adr/0022-attachments-in-azure-blob-storage.md); the middleware order, the body limit and the request timeout are in [request pipeline](request-pipeline.md).

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

Default codes, used when nothing more specific was set: `request.invalid` (400), `request.unauthenticated` (401), `request.forbidden` (403), `resource.not-found` (404), `request.method-not-allowed` (405), `request.too-large` (413), `request.unsupported-media-type` (415), `rate-limit.exceeded` (429), `service.unavailable` (503), `request.timeout` (504), `request.rejected` (any other 4xx) and `server.error` (any other 5xx). Codes raised by the platform: `request.host-not-allowed` (a `Host` outside `Erp:Platform:Host:AllowedHosts`), `request.malformed` (a value the binder cannot read, such as `?take=many` or a body that is not JSON; `RouteHandlerOptions.ThrowOnBadRequest` is on in every environment so these always reach `GlobalExceptionHandler`), `feature.disabled` (the module's flag is off), `idempotency.*` and `query.*` (see [application pipeline](application-pipeline.md)). A failing readiness check answers 503 `service.unavailable` and names no check, because `/healthz/ready` is anonymous; a healthy or degraded report stays a plain-text status word. Platform problems are written with `IProblemDetailsService.TryWriteAsync`, so a caller whose `Accept` header admits no JSON keeps the status and receives the status code pages plain-text body instead of a 500.

A replayed idempotent response (`Idempotency-Replayed: true`) carries the stored body with its `traceId` rewritten to the replay's correlation id; the log line `Replayed the stored problem of request <original> as request <replay>` links the two. A module's own codes are `<aggregate>.<reason>` in kebab case, for example `invoice.already-issued`.

A failed `Result` takes its status from the error's `ErrorKind` (`ResultExtensions.ToProblem`): `Validation` 400, `Forbidden` 403, `NotFound` 404, `Conflict` 409, `TooLarge` 413, `UnsupportedType` 415, `Failure` 422. `TooLarge` and `UnsupportedType` (`Error.TooLarge`, `Error.UnsupportedType`) exist for content a handler refuses after reading it, such as `attachment.too-large` for a file over the configured limit, `attachment.extension-mismatch` for a file name whose extension contradicts the declared type and `attachment.content-mismatch` for bytes that contradict it; the transport's own refusals keep the default codes `request.too-large` and `request.unsupported-media-type`.

## JSON on the wire

`AddErpEndpoints` configures `HttpJsonOptions` once for every endpoint, and the OpenAPI document describes exactly that shape:

| Rule | Setting | Effect |
|---|---|---|
| Numbers are JSON numbers | `NumberHandling = JsonNumberHandling.Strict` | `"5"` is refused where `5` is expected, so every member has one type in the document |
| Dictionary keys are camelCase | `DictionaryKeyPolicy = JsonNamingPolicy.CamelCase` | the `errors` of a validation problem are keyed by the camelCase member name |
| Enums are camelCase names | `JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)` | `AttachmentScanStatus.NotScanned` travels as `"notScanned"`; a number is refused on input. The document declares `enum: ["notScanned", "clean"]`, and the generated client exposes the names as the constant object `AttachmentScanStatusObject`, so the web app compares names, never numbers. Renaming an enum member is therefore a wire change and updates the web app in the same change |

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

A list endpoint takes the shared request record `PagingParameters` (`BuildingBlocks.Endpoints/Paging`) with `[AsParameters]`. Its members carry the same attributes, so every paged route documents the same four query parameters: `page` ("Page number, from 1; the first page when absent."), `pageSize` ("Items per page, from 1 to 200; 50 when absent."), `sort` (comma-separated `field:asc` or `field:desc` terms over the endpoint's allow-list) and `filter` (semicolon-separated `field:operator:value` terms, values percent-encoded, an `in` list separated by `|`). `ToListRequest()` parses them and the handler resolves them against its allow-lists; the grammar and the `query.*` codes are in [application pipeline](application-pipeline.md). The answer is `PagedResponse<T>`: `{ items, page, pageSize, totalCount, totalPages }`.

## Files on the wire

Uploads and downloads are the two routes that do not exchange JSON; both belong to `Platform/Attachments` ([attachments](attachments.md)).

| | Upload: `POST /api/platform/attachments` | Download: `GET /api/platform/attachments/{id}/content?link=` |
|---|---|---|
| Body | `multipart/form-data`; the first part with a file name is the file, read as it arrives with `MultipartReader` over `Request.Body`; the endpoint never binds `IFormFile` and never buffers the file | the decrypted file, streamed with `TypedResults.Stream` |
| Headers | any `Content-Type` other than `multipart/form-data` with a boundary of at most 70 characters answers 415 `attachment.multipart-required` | the stored content type, `Content-Disposition: attachment` with `filename` and `filename*` (UTF-8), `Content-Length` set explicitly from the recorded size because the decrypting stream cannot seek; no `Accept-Ranges` and no range requests |
| Limits | body `MaxSizeBytes` + 64 KiB through the endpoint's `IRequestSizeLimitMetadata` ([request pipeline](request-pipeline.md), "Transport limits"); timeout policy `platform.attachments.transfer`; rate-limiter policy `platform.attachments.uploads`, at most `MaxConcurrentUploads` uploads in flight per process ([request pipeline](request-pipeline.md), "Rate limits") | timeout policy `platform.attachments.transfer` |
| Answer | 201 with `Location` and the attachment; 400, 413, 415 and 422 problems (`attachment.*`, including 415 `attachment.extension-mismatch` for a file name without an extension of the declared type, plus 413 `request.too-large` when the body passes the endpoint's limit); 429 `rate-limit.exceeded` with `Retry-After` when the concurrency cap is reached | 200, or 404 `attachment.not-found` for every refusal after request validation; a missing or over-long `link` is the 400 validation problem `request.invalid` of the request record `RedeemDownloadLinkRequest` |
| Idempotency | no `Idempotency-Key` (ADR-0022) | a GET, handled by the command `RedeemDownloadLinkCommand` because it records a redemption |
| Document | an operation transformer declares the `multipart/form-data` request body (an object with the required binary member `file`); `ProducesProblem` adds 413, 415, 422 and 429 | `Produces<Stream>(200, "application/octet-stream")` |
| Web app | `uploadFile` in `frontend/apps/web/src/shared/api/upload.ts` posts a `FormData` with `XMLHttpRequest` from the browser to `/api` on the web origin, for progress and so no Server Function holds the file in the web server's memory (`next dev` and `next start` still hold the proxied body in memory up to `experimental.proxyClientMaxBodySize`, `"101mb"`, and the image's standalone server up to the 10 MB default; in production the edge proxy sends `/api/*` straight to the API); errors become `ApiError` through `problemFromBody` | the row opens the path that `POST …/download-links` returned (Server Function `createDownloadLink`) through a temporary same-origin `<a download>`, a same-origin request to `/api` that carries the session cookie once the authentication phase issues one; the browser saves the file without leaving the page, and a refused redemption never replaces the page with the problem body |

A link that must be followed by a browser (a download) is a path the API builds with `LinkGenerator.GetPathByName` from a named route (`.WithName(…)`), never a string the handler assembles, so a path base or a route change cannot break it. A secret in a query string, like the download token, is safe from telemetry only because the ASP.NET Core instrumentation redacts query values and the framework's request lines (`Microsoft.AspNetCore.Hosting.Diagnostics`) stay at `Warning` ([observability](../guides/observability.md)); it still reaches browser history and any access log that records query strings, so such a token is single-purpose, short-lived and bound to the actor.

## The OpenAPI document

- Built by `Hosts/Api/OpenApi/OpenApiSetup` with `ErpOpenApiOptions.Configure`: OpenAPI 3.1, title `ERP-AI-Pro API`, version `1.0`, no `servers` entry. Development serves it at `/openapi/erp.json` with the Scalar reference at `/scalar`; Production maps neither.
- Every module route group documents the 400 validation problem and the 404, 429 (`rate-limit.exceeded`), 500 and 504 (`request.timeout`) problems; an endpoint declares any other status it answers with `ProducesProblem`.
- Operations are described with `.WithName("<Domain>.<Module>.<Action>")` and `.WithSummary(…)`. Response records stay internal to the module, so their XML comments do not reach the document.
- `docs/openapi/erp.json` is the committed contract. After changing an endpoint, refresh it; the test host needs `ERP_TEST_SQL_CONNECTION` and `ERP_TEST_BLOB_EMULATOR_HOST` with a running Azurite, like every host test ([testing](../guides/testing.md)):

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
| Web entry point | `frontend/apps/web/src/shared/api/client.ts` (`apiClient()`, `callApi()` for calls that answer a body, `sendApi()` for calls that answer none, such as a 204), the only HTTP client of the web app; the one exception is the browser-side file upload, `uploadFile` in `shared/api/upload.ts` (section "Files on the wire") |
| Errors | `frontend/apps/web/src/shared/api/problem-details.ts` (`ApiError`, `toApiError`, and `problemFromBody` for a problem body read outside the generated client) |

Regenerate after refreshing the document, or both in one go:

```
node scripts/api-client/generate.ts                 # refreshes docs/openapi/erp.json (needs ERP_TEST_SQL_CONNECTION and ERP_TEST_BLOB_EMULATOR_HOST), then regenerates
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
