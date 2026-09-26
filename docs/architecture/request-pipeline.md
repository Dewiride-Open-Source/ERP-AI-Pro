# Request pipeline

The single documented order of the API's middleware. The decision and its reasons are in [ADR-0018](../adr/0018-transport-security-and-the-request-pipeline-order.md); what each response carries is in [HTTP conventions](http-conventions.md). Every row below is composed by `RequestPipeline.UseRequestPipeline` (`backend/Hosts/Api/Dewiride.Erp.Host.Api/Pipeline/RequestPipeline.cs`) and `ErpEndpointsExtensions.UseErpEndpointPipeline` (`backend/BuildingBlocks/Endpoints/Dewiride.Erp.BuildingBlocks.Endpoints/ErpEndpointsExtensions.cs`).

## Order

| # | Middleware | Why here | Proved by |
|---|---|---|---|
| 0 | Framework host filter (`WebApplication.CreateBuilder` startup filter) | Always present ahead of the whole pipeline; it stays permissive (`*`) because the root `AllowedHosts` key is never set, so it rejects nothing | `AllowedHostsTests.Get_HostOffTheList_AnswersAHostProblemWithTheCorrelationAndSecurityHeaders` (a rejection arrives as our problem, not the framework's HTML) |
| 1 | `UseForwardedHeaders` | Before anything that reads the client address or scheme; trusts only peers inside `Erp:Platform:Host:KnownNetworks` with `ForwardLimit` 1, and nobody when the list is empty | `ForwardedHeadersTests` on a real Kestrel listener: known network, unknown network, empty list, two hops, two trusted hops stopping after one (`ForwardLimit` 1), `X-Forwarded-Host` ignored |
| 2 | Configuration refresh (App Configuration source only) | A request that fails later still keeps configuration current; throttled | `ConfigurationRefreshTests` |
| 3 | `CorrelationIdMiddleware` | Every later response, error or not, carries `X-Correlation-ID` and logs under its scope | `CorrelationIdTests`, `AllowedHostsTests`, `RequestTimeoutTests` |
| 4 | `SecurityHeadersMiddleware` | Every later response carries the API headers, including errors | `TransportSecurityTests`, `AllowedHostsTests`, `RequestTimeoutTests` |
| 5 | `UseExceptionHandler` | Wraps everything after it, so an exception becomes a problem body with the headers above | `ExceptionHandlingTests`, `GlobalExceptionHandlerTests` |
| 6 | `UseStatusCodePages` | Turns an empty 4xx/5xx written later (unmatched route, binder 413, timeout 504) into a problem body | `ProblemDetailsTests`, `RequestLimitsTests`, `RequestTimeoutTests` |
| 7 | `AllowedHostsMiddleware` | Enforces `Erp:Platform:Host:AllowedHosts` inside the correlation, header and problem middleware, answering 400 `request.host-not-allowed` (a caller that accepts no JSON gets the status code pages plain-text 400 with the same headers) | `AllowedHostsTests` |
| 8 | `UseRouting` (explicit) | Everything after it sees the endpoint and its metadata | `RequestTimeoutTests.Get_Endpoint_RunsUnderTheDefaultTimeoutUnlessItsMetadataDisablesIt`, `FeatureEndpointsTests` |
| 9 | `UseRequestTimeouts` | After routing so per-endpoint metadata applies; inside status code pages so the 504 becomes a `request.timeout` problem | `RequestTimeoutTests` |
| 10 | `DatabaseCancellationMiddleware` | Inside request timeouts: a `DbException` (or an exception wrapping one) raised after `RequestAborted` was cancelled is rethrown as `OperationCanceledException`, the only exception the timeout middleware (504) and the exception handler (499 for a caller that has gone) recognise; SqlClient reports a cancelled command as `SqlException` | `RequestTimeoutTests` (`WAITFOR DELAY` past the timeout answers 504), `DatabaseCancellationMiddlewareTests` |
| — | *authentication (authentication phase)* | After routing and before the rate limiter, so a signed-in caller is limited as that actor; called explicitly, because with an explicit `UseRouting` .NET 10 would otherwise insert it ahead of the whole pipeline with no endpoint | — |
| 11 | `UseErpRateLimiting` (`UseRateLimiter` with the global limiter only) | After forwarded headers so an anonymous caller is counted by its real address; after routing so the health endpoints' `DisableRateLimiting` metadata applies; before the feature gate and idempotency so a limited caller costs nothing further | `RateLimitingTests` (forwarded clients each have their own allowance; a disabled module is limited before the gate answers; the health endpoints are exempt), `GlobalRateLimiterTests` |
| — | *authorization → antiforgery (authentication phase)* | After routing and the rate limiter; called explicitly for the same reason as authentication | — |
| 12 | `UseFeatureGate` | Reads the module's `RequireFeature` metadata; a disabled module answers 404 `feature.disabled` before binding | `FeatureEndpointsTests`, `FeatureGateMiddlewareTests` |
| 13 | `UseErpIdempotency` | Reads `RequireIdempotencyKey` metadata; runs after the gate so a disabled module never claims a key | `IdempotencyMiddlewareTests`, `FeatureGateOrderTests` (a disabled module answers `feature.disabled` with no key and never replays a key) |
| 14 | Endpoints (added by `WebApplication`) | — | every endpoint test |

## Transport limits

| Setting | Effect | Proved by |
|---|---|---|
| `Erp:Platform:Host:MaxRequestBodyBytes` (Kestrel `Limits.MaxRequestBodySize`) | A larger body answers 413 `request.too-large`: the binder writes the empty 413 that status code pages completes, and a manual read throws `BadHttpRequestException` that `GlobalExceptionHandler` maps; both carry the title `Content Too Large`. An endpoint that must accept more (file uploads) adds its own `RequestSizeLimitAttribute` metadata | `RequestLimitsTests` (Kestrel; the in-memory test server ignores the limit) |
| `Erp:Platform:Host:RequestTimeout` (`RequestTimeoutOptions.DefaultPolicy`) | A request still running after it has its `RequestAborted` token cancelled; a handler that observes it ends with 504 `request.timeout`. A database command cancelled by it answers 504 too (row 10). An endpoint opts out with `.DisableRequestTimeout()` or sets its own with `.WithRequestTimeout(...)`. The middleware does nothing while a debugger is attached. The web app's `/api` rewrite waits `experimental.proxyTimeout` (660 000 ms, `frontend/apps/web/next.config.ts`), above the 10-minute upper bound of this setting, so the API's 504 always arrives before the rewrite gives up | `RequestTimeoutTests` |
| Kestrel `AddServerHeader = false` | No `Server` header | `TransportSecurityTests` |

## Rate limits

The global limiter (`GlobalRateLimiter`, `BuildingBlocks.Endpoints/RateLimiting`) gives every request one partition:

| Caller | Partition | Limiter | Settings |
|---|---|---|---|
| Anonymous | `address:<client address>`; an IPv4-mapped address as IPv4, an IPv6 address by its /64 prefix, no address as `unknown` | fixed window | `Erp:Platform:RateLimiting:AnonymousPermitLimit` per `AnonymousWindow` |
| Signed in (`IActorContext.IsAuthenticated`) | `actor:<actor id>` across every address | sliding window | `ActorPermitLimit` per `ActorWindow` in `ActorSegmentsPerWindow` steps |

A signed-in caller is never limited by address, so colleagues behind one office connection keep their own allowances. A rejected request answers 429 `rate-limit.exceeded` with `Retry-After` (whole seconds, at least 1: the wait the rejected lease reports, or one actor segment, `ActorWindow / ActorSegmentsPerWindow`, for a signed-in caller, because a rejected sliding-window lease reports none in .NET 10; `RateLimiterOptionsSetupTests`) and no queueing; every module route group declares the 429 problem. `/healthz/live` and `/healthz/ready` carry `DisableRateLimiting` and `DisableHttpMetrics`, so probes are never limited, spend no allowance and stay out of the request metrics. The limiter is built once at startup; a change to `Erp:Platform:RateLimiting` takes effect at the next start.

The client address is the one forwarded headers resolve (row 1). In production the edge proxy routes `/api/*` straight to the API with the browser's address in `X-Forwarded-For`; the web container's server-side calls forward the last `X-Forwarded-For` entry it received (`frontend/apps/web/src/shared/api/forwarded-headers.ts`), so pages rendered on the server are counted per visitor rather than as one web container. Both peers sit in `Erp:Platform:Host:KnownNetworks`. Without `KnownNetworks` (local development) every caller is the loopback peer and shares one anonymous allowance.

## Health

`/healthz/live` runs no check. `/healthz/ready` runs every check tagged `ready` — `self`, one `database:<schema>` check per catalogue context and, with the App Configuration source, `app-configuration` — each under `HealthEndpoints.CheckTimeout` (2 seconds, below the container probe's 3-second client timeout) unless the check sets its own. A healthy or degraded report answers 200 with the status word; an unhealthy one answers 503 `service.unavailable` naming no check, because the endpoint is anonymous; the failing check and its exception are in the log. The App Configuration check reports `Degraded` rather than `Unhealthy`: the API keeps serving from the configuration it last loaded. Proved by `HealthEndpointsTests` (host: a hung check and an unreachable SQL Server both answer 503 within the bound, and without the timeout the unreachable server took 30 seconds; unit: timeouts and failure status).

## What the API never does

- No `UseHttpsRedirection` and no `UseHsts`: the edge proxy owns TLS, the HTTPS redirect and HSTS (`TransportSecurityTests` in Development and Production: plain HTTP with `https_port` known is served without a redirect, and HTTPS forwarded by a trusted proxy for a public host name carries no `Strict-Transport-Security`; both fail when either call is added).
- No `ASPNETCORE_FORWARDEDHEADERS_ENABLED`: that switch makes the framework trust forwarded headers from every peer; it is never set in any compose file or environment.
- No output caching middleware: every `/api` response is `no-store` except a successful GET of an endpoint marked `.WithReferenceDataCaching(maxAge)`, which answers `Cache-Control: private, max-age=<seconds>` (`ReferenceDataCachingTests`); reference data that must be cached on the server goes through `HybridCache` (`Erp:Platform:Caching`).
- No explicit `UseHostFiltering`: it would add a second copy of the framework filter, whose rejection is an HTML page without the API headers.

Responses that never reach this pipeline carry none of the headers or the problem body: Kestrel's own protocol rejections (malformed request line, oversized headers, 431/414) are written by the server before any middleware runs.
