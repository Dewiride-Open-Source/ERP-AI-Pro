# ADR-0020: Observability baseline

Status: accepted
Date: 2026-09-26

## Context

The API already registered OpenTelemetry for ASP.NET Core and HttpClient with the OTLP exporter behind `OTEL_EXPORTER_OTLP_ENDPOINT`, a `Dewiride.Erp.*` source wildcard and the `OpenTelemetry.Instrumentation.Runtime` package. The roadmap asks for traces, metrics and logs over OTLP, logging conventions enforced by analyzers, ASP.NET Core, Kestrel, HttpClient and EF Core meters, the standard resilience handler for outbound HTTP, and the local Aspire dashboard receiving telemetry. Facts that shape the decision:

- .NET 10 and ASP.NET Core 10 publish their metrics on named meters (`Microsoft.AspNetCore.Hosting`, `.Server.Kestrel`, `.MemoryPool`, `.Routing`, `.Diagnostics`, `.RateLimiting`, `.Authentication`, `.Authorization`, `System.Net.Http`, `System.Net.NameResolution`, `System.Runtime`), and EF Core 10 on `Microsoft.EntityFrameworkCore`. `System.Runtime` has been built in since .NET 9, so the runtime instrumentation package adds nothing.
- Microsoft.Data.SqlClient 7.1 and EF Core 10 start no activities; SQL spans and `db.client.operation.duration` need `OpenTelemetry.Instrumentation.SqlClient` (1.19.0, stable, Apache-2.0), which records the parameterised command text and records parameter values only when `OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS` is set. `OpenTelemetry.Instrumentation.EntityFrameworkCore` is published only as a prerelease.
- The resource service version came from the Observability assembly rather than the host, and no deployment environment was reported.
- `.editorconfig` switched `CA1848` off, and nothing enforced constant logging templates, although every existing log call already uses `[LoggerMessage]`.
- `Microsoft.Extensions.Http.Resilience` 10.10.0 (MIT) depends on Polly (`Polly.Core`, `Polly.Extensions`, `Polly.RateLimiting`, BSD-3-Clause, App vNext, a .NET Foundation project). Its standard handler retries every method by default. Polly publishes its events on the meter `Polly`.

## Decision

- **Metrics come from an explicit list.** `TelemetryMeters.Names` lists the ASP.NET Core and .NET meters above plus `Dewiride.Erp.*`; `AddErpDatabaseTelemetry` adds `Microsoft.EntityFrameworkCore` and the SqlClient instrumentation; `AddErpHttpClientDefaults` adds `Polly`. `OpenTelemetry.Instrumentation.Runtime` is removed. The ASP.NET Core and HttpClient instrumentation packages stay for tracing, where they filter `/healthz/*` and follow the semantic conventions.
- **SQL telemetry lives with persistence.** `AddErpDatabaseTelemetry` (`BuildingBlocks.Persistence/Telemetry`), called by `AddErpPersistence`, registers SqlClient tracing and metrics and the EF Core meter through `ConfigureOpenTelemetryTracerProvider` and `ConfigureOpenTelemetryMeterProvider`, so every host that composes persistence reports them. `OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS` is never set, because parameter values carry PAN, bank numbers and salaries.
- **The resource identifies the running host.** `service.name` from `Erp:Platform:Host:ApplicationName`, `service.instance.id` from the machine or container name, and `ErpResourceDetector` adds `service.version` (the host's informational version from `ApplicationInfo`) and `deployment.environment.name` (the App Configuration label, else the host environment name).
- **Logging conventions are errors.** `CA1848` and `CA2254` are `error` in `.editorconfig`, so every log call is a `[LoggerMessage]` source-generated method with a constant template. Logs keep `IncludeScopes`, so each entry carries `CorrelationId`, `RequestPath`, `TraceId` and `SpanId`.
- **Outbound HTTP is resilient by default.** `AddErpHttpClientDefaults` (`BuildingBlocks.Observability/Resilience`), called by `AddErpPlatform` for every host, adds the standard resilience handler to every `IHttpClientFactory` client through `ConfigureHttpClientDefaults`, with `Retry.DisableForUnsafeHttpMethods()` so POST, PUT, PATCH, DELETE and CONNECT are never retried. The owner approved Polly on 2026-09-26; `Polly.Core`, `Polly.Extensions` and `Polly.RateLimiting` are pinned at 8.8.0 in `Directory.Packages.props` (transitive pinning), above the 8.4.2 minimum the resilience package declares.
- **Export stays opt-in.** The OTLP exporter is added only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; code never references an exporter. Locally the Aspire dashboard (compose profile `observability`) receives telemetry from the containers.

## Consequences

- A module adds spans or measurements through its own `ActivitySource` or `Meter` named `Dewiride.Erp.<Domain>.<Module>` and needs no registration; it never adds an instrumentation package.
- Outbound calls that change data are attempted once; the caller decides how to recover, typically with an idempotency key.
- Any new meter the platform wants exported (for example SignalR's) must be added to `TelemetryMeters.Names`; an unlisted meter is not exported.
- The local verification is recorded in the pull request: the dashboard showed `ERP-AI-Pro: GET /api/platform/system-info/startups` with its `ListRecentStartupsQuery` span and the `SELECT [platform_system_info].[Startups]` SQL span, and the SQL command log carried the caller's `X-Correlation-ID`.
- `OpenTelemetry.Instrumentation.EntityFrameworkCore` is revisited when it ships a stable release; until then EF Core appears in traces through its SQL commands.
