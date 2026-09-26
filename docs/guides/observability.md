# Observability

What the API records about itself, how to look at it locally, and the rules that keep personal data out of it. The decision and its reasons are in [ADR-0020](../adr/0020-observability-baseline.md).

## What the API emits

Everything goes through OpenTelemetry, set up once by `AddErpTelemetry` (`backend/BuildingBlocks/Observability/Dewiride.Erp.BuildingBlocks.Observability/Telemetry/OpenTelemetrySetup.cs`). Nothing leaves the process unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set; then traces, metrics and logs go to that endpoint over OTLP (gRPC for the API).

| Signal | Sources | Registered by |
|---|---|---|
| Traces | ASP.NET Core requests (except `/healthz/*`), outbound `HttpClient` calls, SQL commands through SqlClient, every `ActivitySource` named `Dewiride.Erp.*` (the application pipeline adds one span per command or query), and the Azure Blob Storage client library's sources `Azure.Storage.Blobs.*` (one span per logical operation, such as `BlockBlobClient.StageBlock` and `BlockBlobClient.CommitBlockList`, above the `HttpClient` span of each storage request) | `AddErpTelemetry`; SQL by `AddErpDatabaseTelemetry`; `Azure.Storage.Blobs.*` by `AddErpAttachments` |
| Metrics | the meters in `TelemetryMeters.Names`: `Microsoft.AspNetCore.Hosting`, `.Server.Kestrel`, `.MemoryPool`, `.Routing`, `.Diagnostics`, `.RateLimiting`, `.Authentication`, `.Authorization`, `System.Net.Http`, `System.Net.NameResolution`, `System.Runtime` and `Dewiride.Erp.*` (which includes the attachments meter `Dewiride.Erp.Attachments`, section "Attachments"); plus `Microsoft.EntityFrameworkCore` and `db.client.operation.duration` (SqlClient), and `Polly` (resilience events) | `AddErpTelemetry`, `AddErpDatabaseTelemetry`, `AddErpHttpClientDefaults` |
| Logs | every `ILogger` category at its configured level, with the formatted message, the structured values and the scopes, so each entry carries `CorrelationId`, `RequestPath`, `TraceId` and `SpanId` | `AddErpTelemetry` |

The health endpoints are left out of request traces and request metrics (`DisableHttpMetrics`), so probes do not drown real traffic.

Every signal carries the same resource: `service.name` (`Erp:Platform:Host:ApplicationName`), `service.version` (the host assembly's informational version), `service.instance.id` (the machine or container name) and `deployment.environment.name` (the App Configuration label, `local-dev` or `production`, or the host environment name when no store is connected).

## Rules for code

- Log only through `[LoggerMessage]` source-generated methods with constant templates. `CA1848` and `CA2254` are build errors, so `logger.LogInformation(...)` and interpolated templates do not compile.
- Never log a token, a secret, PAN, Aadhaar, a bank account number, a salary component or a request body; pass identifiers, never the values.
- A module that wants its own spans or measurements creates an `ActivitySource` or a `Meter` (through `IMeterFactory`) named `Dewiride.Erp.<Domain>.<Module>`, a building block one named `Dewiride.Erp.<Concern>` (`Dewiride.Erp.Attachments`); the `Dewiride.Erp.*` wildcard collects it. Modules never add OpenTelemetry instrumentation packages themselves; a building block that wraps an Azure client library adds that library's `ActivitySource` names with `ConfigureOpenTelemetryTracerProvider` from its own registration, as `AddErpAttachments` does.
- Outbound HTTP goes through `IHttpClientFactory`. Every client gets the standard resilience handler from `AddErpHttpClientDefaults`: at most 1 000 concurrent requests, a total timeout of 30 seconds, up to 3 retries with exponential back-off from 2 seconds, a circuit breaker and a per-attempt timeout of 10 seconds, with retries for safe methods only (GET, HEAD, OPTIONS, TRACE). A POST, PUT, PATCH, DELETE or CONNECT is never retried, because a retry could apply the change twice. The pipeline is selected per scheme, host and port, so one failing dependency opens only its own circuit and never blocks calls to another.

## SQL and personal data

SqlClient 7 and EF Core 10 start no activities of their own, so SQL spans come from the OpenTelemetry SqlClient instrumentation. A span carries `db.system.name`, `db.namespace`, `server.address`, `db.operation.name` and `db.query.text`, the parameterised command text EF Core generates (`SELECT ... WHERE [s].[Id] = @id`); parameter values are not recorded. When a command fails, SQL Server can quote the offending value in its message (a duplicate key, a truncated string) and the instrumentation copies the message into the span status; `SqlErrorStatusProcessor`, registered before any exporter, clears that description and leaves `error.type` and `db.response.status_code` (for example `2627`) to identify the failure. EF Core's own command log shows parameters as `?` because sensitive data logging stays off, but its error entry keeps the exception message, which is why PAN, Aadhaar and bank numbers are stored encrypted through the field-protection abstraction: a quoted value is then ciphertext.

Never set `OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS`: it copies every parameter value into the span, which would export PAN, bank numbers and salaries. `DatabaseTelemetryTests` proves a parameter value never reaches a span.

Write queries so personal data travels as parameters (LINQ over captured variables, never string-built SQL), so the query text stays free of values.

## Attachments

What the attachments building block records ([attachments](../architecture/attachments.md)):

| Instrument (meter `Dewiride.Erp.Attachments`) | Type, unit | Tags | Recorded |
|---|---|---|---|
| `erp.attachments.uploads` | counter, `{upload}` | `erp.attachments.outcome` (`stored`, `deduplicated`, `rejected`); `erp.error.code` on `rejected` (for example `attachment.too-large`) | every upload the uploader accepts or rejects through a result; the endpoint's own refusals (`attachment.multipart-required`, `attachment.file-missing`, `attachment.incomplete`), `request.too-large` and exceptions are not counted |
| `erp.attachments.upload.size` | histogram, `By` | — | the size of every accepted upload |
| `erp.attachments.downloads` | counter, `{download}` | `erp.attachments.outcome` (`served`, `refused`) | every attempt to redeem a download link that passes request validation and ends in a download or a 404; a storage exception (500) is not counted |
| `erp.attachments.reservations.swept` | counter, `{reservation}` | — | abandoned upload reservations whose blobs `AttachmentsSweeper` removed |
| `erp.attachments.links.purged` | counter, `{link}` | — | download links `AttachmentsSweeper` deleted because they expired more than an hour ago without a redemption |

An upload refused by the concurrency cap (429 `rate-limit.exceeded`) never reaches the uploader, so `erp.attachments.uploads` does not count it; the `Microsoft.AspNetCore.RateLimiting` meter does, in `aspnetcore.rate_limiting.requests` with `aspnetcore.rate_limiting.policy` `platform.attachments.uploads`, and `aspnetcore.rate_limiting.active_request_leases` shows the uploads in flight.

- Logs name the attachment id, the content id, the length and the key id, never a file name, a link token or file content: `Stored attachment {AttachmentId} as content {ContentId} ({Length} bytes, key {KeyId}, deduplicated: {Deduplicated})`, `Attachment storage ready at {ContainerUri} with encryption key {KeyId} …` at startup, `Removed the blobs of {Removed} abandoned attachment uploads and {Purged} unused download links` when a sweep removed something, warnings when a virus scanner rejects an upload, a cleanup is deferred to the sweeper or a sweep fails, and errors when a stored content has no blob, its envelope fails authentication or its header cannot be opened with the configured keys (`StoredContentVerifier`, checked before a download link is created and before stored content is reused; each answers 404 to the caller or makes the upload keep its own copy, so the error log is where damage, a restore without its blobs or a changed key shows up).
- Storage spans: each storage request is an `HttpClient` span whose `url.full` names the account, the container and the blob, a random content id that reveals nothing about the file. On .NET 9 and later the runtime creates the `HttpClient` span itself and redacts `url.full`: the whole query becomes `*` and user info and fragment are dropped; `OpenTelemetry.Instrumentation.Http` leaves the runtime's attributes unchanged, so its `OTEL_DOTNET_EXPERIMENTAL_HTTPCLIENT_DISABLE_URL_QUERY_REDACTION` has no effect here, and the runtime's opt-out (`DOTNET_SYSTEM_NET_HTTP_DISABLEURIREDACTION`, the `System.Net.Http.DisableUriRedaction` switch) is never set. The client library's content logging stays at its default (off) and its event-source logs are not forwarded.
- The download token travels in the query string of `GET /api/platform/attachments/{id}/content?link=<token>`. The ASP.NET Core instrumentation records `url.query` with every value replaced by `Redacted` (its default). Never set `OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION`: it would copy every download token into the server span. The framework's request-start and request-finish lines (category `Microsoft.AspNetCore.Hosting.Diagnostics`, `Request starting …` and `Request finished …`) print the path with its query string, token included, at `Information`; `appsettings.json` keeps all of `Microsoft.AspNetCore` at `Warning`, and `appsettings.Development.json`, which raises `Microsoft.AspNetCore` to `Information` for local diagnosis, keeps `Microsoft.AspNetCore.Hosting.Diagnostics` at `Warning`. Never lower that category below `Warning` in any configuration source. `TelemetryTests.Get_AttachmentContent_ExportsNoSpanOrLogCarryingTheLinkToken` (the test host runs as `Development`) proves the server span carries `url.query` and that neither a span of the trace (name, status, tags, events, links, baggage) nor an exported log record (message, body, attributes, exception) carries the token.
- Readiness: `storage:attachments` (tag `ready`) reads the container's properties through a client with no retries and the 2-second `HealthEndpoints.CheckTimeout` as network timeout, and reports `Degraded` when storage is unreachable or the container is missing, so `/healthz/ready` answers 200 `Degraded` and every non-attachment route keeps serving. The failing check and its exception are in the log.

## Seeing it locally

The Aspire dashboard runs as the `aspire-dashboard` service under the compose profile `observability` and receives OTLP from the containers on the compose network.

1. In `infra/compose/.env` set `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889` (API, gRPC) and `WEB_OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18890` (web app, OTLP/HTTP); `.env.example` has both lines commented.
2. Start the stack: `docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml --profile observability up -d --wait --build`.
3. Call the API, for example `curl -H "X-Correlation-ID: my-check-1" http://127.0.0.1:5080/api/platform/system-info/startups`.
4. Open `http://localhost:18888`. **Traces** shows `ERP-AI-Pro: GET /api/platform/system-info/startups` with its `ListRecentStartupsQuery` span and the `SELECT [platform_system_info].[Startups]` SQL span beneath it. **Structured logs** shows the request's entries; open `Executed DbCommand` and its `CorrelationId` is `my-check-1` and its trace link opens the same trace.
5. Stop with `docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml --profile observability down`.

The dashboard runs without sign-in (`ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`) and publishes only its UI port, on `127.0.0.1`; it exists in the local stack only. A host-process API (`dotnet run`) exports nothing unless you point `OTEL_EXPORTER_OTLP_ENDPOINT` at a collector you run yourself, because the dashboard's OTLP ports are not published to the host.

## How it is tested

| Test | Proves |
|---|---|
| `OpenTelemetrySetupTests` | the OTLP exporter is registered only when the endpoint is set; a listed meter (including a `Dewiride.Erp.*` one) is exported and an unlisted one is not; the resource attributes |
| `DatabaseTelemetryTests` (SQL Server) | a SQL command produces a client span whose query text carries no parameter value; a duplicate key the server quotes leaves an error span with no status description and no trace of the value; an EF Core query produces `db.client.operation.duration` and `microsoft.entityframeworkcore.queries` |
| `TelemetryTests` (host) | one request exports its server span and its SQL span in one trace, and the SQL command log carries the request's `CorrelationId` and trace id; an upload's trace contains the `Azure.Storage.Blobs.*` spans `BlockBlobClient.StageBlock` and `BlockBlobClient.CommitBlockList`; a download's server span carries `url.query` while no span of its trace and no exported log record carries the link token |
| `AttachmentsMetricsTests` | every attachments instrument with its tags (and the `By` unit of the size histogram), and that the `Dewiride.Erp.Attachments` meter is covered by `TelemetryMeters.Names` |
| `HealthEndpointsTests` (host) | `storage:attachments` is healthy against the test container, and an unreachable storage endpoint makes `/healthz/ready` answer 200 `Degraded` with only that check failing |
| `HttpClientResilienceTests` | a GET is retried after a 503 and a POST is not; an open circuit for one destination leaves another destination served |
