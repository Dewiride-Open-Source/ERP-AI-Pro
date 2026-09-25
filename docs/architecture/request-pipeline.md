# Request pipeline

The single documented order of the API's middleware. The decision and its reasons are in [ADR-0018](../adr/0018-transport-security-and-the-request-pipeline-order.md); what each response carries is in [HTTP conventions](http-conventions.md). Every row below is composed by `RequestPipeline.UseRequestPipeline` (`backend/Hosts/Api/Dewiride.Erp.Host.Api/Pipeline/RequestPipeline.cs`) and `ErpEndpointsExtensions.UseErpEndpointPipeline` (`backend/BuildingBlocks/Endpoints/Dewiride.Erp.BuildingBlocks.Endpoints/ErpEndpointsExtensions.cs`).

## Order

| # | Middleware | Why here | Proved by |
|---|---|---|---|
| 0 | Framework host filter (`WebApplication.CreateBuilder` startup filter) | Always present ahead of the whole pipeline; it stays permissive (`*`) because the root `AllowedHosts` key is never set, so it rejects nothing | `AllowedHostsTests.Get_HostOffTheList_AnswersAHostProblemWithTheCorrelationAndSecurityHeaders` (a rejection arrives as our problem, not the framework's HTML) |
| 1 | `UseForwardedHeaders` | Before anything that reads the client address or scheme; trusts only peers inside `Erp:Platform:Host:KnownNetworks` with `ForwardLimit` 1, and nobody when the list is empty | `ForwardedHeadersTests` (known network, unknown network, empty list, two hops) on a real Kestrel listener |
| 2 | Configuration refresh (App Configuration source only) | A request that fails later still keeps configuration current; throttled | `ConfigurationRefreshTests` |
| 3 | `CorrelationIdMiddleware` | Every later response, error or not, carries `X-Correlation-ID` and logs under its scope | `CorrelationIdTests`, `AllowedHostsTests`, `RequestTimeoutTests` |
| 4 | `SecurityHeadersMiddleware` | Every later response carries the API headers, including errors | `TransportSecurityTests`, `AllowedHostsTests`, `RequestTimeoutTests` |
| 5 | `UseExceptionHandler` | Wraps everything after it, so an exception becomes a problem body with the headers above | `ExceptionHandlingTests`, `GlobalExceptionHandlerTests` |
| 6 | `UseStatusCodePages` | Turns an empty 4xx/5xx written later (unmatched route, binder 413, timeout 504) into a problem body | `ProblemDetailsTests`, `RequestLimitsTests`, `RequestTimeoutTests` |
| 7 | `AllowedHostsMiddleware` | Enforces `Erp:Platform:Host:AllowedHosts` inside the correlation, header and problem middleware, answering 400 `request.host-not-allowed` | `AllowedHostsTests` |
| 8 | `UseRouting` (explicit) | Everything after it sees the endpoint and its metadata | `RequestTimeoutTests.Get_EndpointThatDisablesTheTimeout_RunsPastIt`, `FeatureEndpointsTests` |
| 9 | `UseRequestTimeouts` | After routing so per-endpoint metadata applies; inside status code pages so the 504 becomes a `request.timeout` problem | `RequestTimeoutTests` |
| — | *rate limiting (`backend-platform-rate-limiting-output-caching-and-health`)* | After routing so endpoint policies and `DisableRateLimiting` apply; after forwarded headers so partitions see the client address | — |
| — | *authentication → authorization → antiforgery (authentication phase)* | After routing; called explicitly, because with an explicit `UseRouting` .NET 10 would otherwise insert them ahead of the whole pipeline with no endpoint | — |
| 10 | `UseFeatureGate` | Reads the module's `RequireFeature` metadata; a disabled module answers 404 `feature.disabled` before binding | `FeatureEndpointsTests`, `FeatureGateMiddlewareTests` |
| 11 | `UseErpIdempotency` | Reads `RequireIdempotencyKey` metadata; runs after the gate so a disabled module never claims a key | `IdempotencyMiddlewareTests` |
| 12 | Endpoints (added by `WebApplication`) | — | every endpoint test |

## Transport limits

| Setting | Effect | Proved by |
|---|---|---|
| `Erp:Platform:Host:MaxRequestBodyBytes` (Kestrel `Limits.MaxRequestBodySize`) | A larger body answers 413 `request.too-large`: the binder writes the empty 413 that status code pages completes, and a manual read throws `BadHttpRequestException` that `GlobalExceptionHandler` maps; both carry the title `Content Too Large`. An endpoint that must accept more (file uploads) adds its own `RequestSizeLimitAttribute` metadata | `RequestLimitsTests` (Kestrel; the in-memory test server ignores the limit) |
| `Erp:Platform:Host:RequestTimeout` (`RequestTimeoutOptions.DefaultPolicy`) | A request still running after it has its `RequestAborted` token cancelled; a handler that observes it ends with 504 `request.timeout`. An endpoint opts out with `.DisableRequestTimeout()` or sets its own with `.WithRequestTimeout(...)`. The middleware does nothing while a debugger is attached | `RequestTimeoutTests` |
| Kestrel `AddServerHeader = false` | No `Server` header | `TransportSecurityTests` |

## What the API never does

- No `UseHttpsRedirection` and no `UseHsts`: the edge proxy owns TLS, the HTTPS redirect and HSTS (`TransportSecurityTests` in Development and Production).
- No `ASPNETCORE_FORWARDEDHEADERS_ENABLED`: that switch makes the framework trust forwarded headers from every peer; it is never set in any compose file or environment.
- No explicit `UseHostFiltering`: it would add a second copy of the framework filter, whose rejection is an HTML page without the API headers.

Responses that never reach this pipeline carry none of the headers or the problem body: Kestrel's own protocol rejections (malformed request line, oversized headers, 431/414) are written by the server before any middleware runs.
