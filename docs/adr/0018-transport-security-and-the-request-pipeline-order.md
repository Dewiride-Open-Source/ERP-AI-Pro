# ADR-0018: Transport security and the request pipeline order

Status: accepted
Date: 2026-09-25

## Context

The API runs behind the edge proxy (TLS, HTTPS redirect, HSTS) and, in local development, behind the web server's `/api` rewrite. The request pipeline had grown by accretion: routing was inserted implicitly by `WebApplication` at the head of the pipeline, forwarded headers were configured from `Erp:Platform:Host:KnownNetworks`, host filtering was added explicitly, and there was no request body limit or request timeout beyond the framework defaults. Root `CLAUDE.md` §9 fixes the intended order and header ownership, and the roadmap asks for each header and proxy scenario to be proven.

Reading the ASP.NET Core 10 sources and probing them showed four facts the old composition did not account for:

- `ForwardedHeadersMiddleware` trusts **every** peer when both `KnownIPNetworks` and `KnownProxies` are empty. The host cleared both and then added the configured networks, so an unset `KnownNetworks` (local development, and any environment that forgot it) let any caller claim any client address and scheme.
- `System.Net.IPNetwork.Parse` in .NET 10 clears host bits instead of rejecting them, so a mistyped `172.28.0.5/16` silently trusts the whole `/16`; and an invalid entry only failed at the first request, when the options were first built.
- `WebApplication.CreateBuilder` always runs its own host filter ahead of the whole pipeline, sharing `HostFilteringOptions` with any explicit `UseHostFiltering`. A rejected host therefore received the framework's HTML 400 without the correlation id, the API security headers or a problem body. ADR-0011 records this as the behaviour; this ADR supersedes that consequence.
- With an explicit `UseRouting`, .NET 10 still inserts `UseAuthentication`/`UseAuthorization` automatically at the head of the pipeline (before routing, with no endpoint) unless the app calls them itself; dotnet/aspnetcore#67307 fixes this only in .NET 11.

Two facts shape the tests: the in-memory test server has no client address (the forwarded-headers trust check is skipped when the address is null) and ignores Kestrel limits, while `WebApplicationFactory` in .NET 10 can run a real Kestrel listener (`UseKestrel(0)`).

## Decision

- The order in [`docs/architecture/request-pipeline.md`](../architecture/request-pipeline.md) is the single documented order: forwarded headers → configuration refresh → correlation id → security headers → exception handler → status code pages → allowed hosts → explicit `UseRouting` → request timeouts → (rate limiting, then authentication → authorization → antiforgery, each called explicitly when they arrive) → feature gate → idempotency → endpoints. Each adjacency that matters is proven by a named integration test in that document.
- Forwarded headers are processed only from peers inside `Erp:Platform:Host:KnownNetworks`, with `X-Forwarded-For` and `X-Forwarded-Proto` and `ForwardLimit` 1; an empty list sets `ForwardedHeaders.None`, so no proxy is trusted. `ErpHostOptionsValidator` (validated at startup) rejects an entry that is not CIDR notation with its exact network address, naming the entry. `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is never set.
- Host validation is `AllowedHostsMiddleware` in `BuildingBlocks.Endpoints`, placed after the problem middleware, using the framework's own matching (`HostString.MatchesAny`: case-insensitive, port ignored, `*.` subdomain wildcards) over `Erp:Platform:Host:AllowedHosts` split on `;` and trimmed. A miss answers 400 `request.host-not-allowed` as a problem with the correlation id and the API headers. The framework's built-in filter stays permissive because the root `AllowedHosts` key is never set; the host no longer calls `UseHostFiltering` or configures `HostFilteringOptions`.
- Kestrel runs with `AddServerHeader = false` and `Limits.MaxRequestBodySize` from `Erp:Platform:Host:MaxRequestBodyBytes` (default 1 MiB, 1 KiB to 1 GiB). A larger body answers 413 `request.too-large` with the title `Content Too Large` whether the binder or the endpoint reads it.
- The request-timeouts middleware runs with a default policy from `Erp:Platform:Host:RequestTimeout` (default 30 seconds, 1 second to 10 minutes). A handler that observes the cancelled token ends with 504, which status code pages turn into a `request.timeout` problem (`ProblemTypes.DefaultCode(504)`). Endpoints opt out with `.DisableRequestTimeout()`.
- `SecurityHeadersMiddleware` adds `Cross-Origin-Resource-Policy: same-origin`, `Cross-Origin-Opener-Policy: same-origin` and `X-Permitted-Cross-Domain-Policies: none` (OWASP Secure Headers Project) to the existing `nosniff`, referrer policy, permissions policy, `default-src 'none'; frame-ancestors 'none'` CSP and `no-store`. The API never calls `UseHttpsRedirection` or `UseHsts`.
- `GlobalExceptionHandler` loses its 499 branch and `ProblemTypes` its `request.cancelled` code: `ExceptionHandlerMiddleware` answers a request whose caller has gone with 499 itself, before any `IExceptionHandler` runs, so the branch was unreachable.
- Transport tests run on a real Kestrel listener through `ErpApiFactory.WithKestrel()` (`UseKestrel(0)`), each with its own factory; `WithWebHostBuilder` is never combined with it, because the derived factory does not carry Kestrel over.

## Consequences

- Local development and every environment without `KnownNetworks` see the direct peer as the client (the web server, over loopback). Per-client rate limiting in the next sub-phase therefore needs `KnownNetworks` in every deployed environment; the compose file already lists the compose network.
- A change to `AllowedHosts`, `KnownNetworks`, `MaxRequestBodyBytes` or `RequestTimeout` takes effect at the next start: Kestrel, forwarded-headers and host options are read once.
- The public host name never reaches the API: the edge proxy talks to the web container, which calls the API as `http://api:8080`, and the container health probe calls `http://127.0.0.1:8080`. When the first-deployment phase narrows `AllowedHosts`, the production value is therefore `api;127.0.0.1`, not the public host name.
- The authentication phase must call `UseAuthentication()`, `UseAuthorization()` and `UseAntiforgery()` explicitly after `UseRouting()`; relying on the automatic insertion would run them before routing.
- A timeout only ends work that observes `RequestAborted`; a handler that ignores the token runs to completion and its response is sent. A database command cancelled by the token may surface as an exception other than `OperationCanceledException`, which becomes a 500 rather than a 504.
- Kestrel's own protocol rejections (malformed request line, oversized headers) are written by the server and carry neither the API headers nor a problem body.
