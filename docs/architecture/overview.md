# Architecture overview

ERP-AI-Pro is a modular monolith: one ASP.NET Core process hosts independent business modules behind one Next.js web application, deployed with Docker Compose on an on-premises Ubuntu server.

```
┌──────────────┐   https    ┌─────────────┐   /api/*   ┌──────────────────────────────────┐
│   Browser    │ ─────────▶ │ Edge proxy  │ ─────────▶ │ Dewiride.Erp.Host.Api             │
│ (cookie only)│            │ TLS + HSTS  │            │  BuildingBlocks + Modules         │
└──────────────┘            │             │   /*       │  Entra ID OIDC (BFF)              │
                            │             │ ─────────▶ │  EF Core → SQL Server / Azure SQL │
                            └─────────────┘            └──────────────────────────────────┘
                                   │                              │
                                   ▼                              ▼
                            ┌─────────────┐            Azure App Configuration + Key Vault
                            │ Next.js web │            (labels local-dev / production)
                            │ standalone  │
                            └─────────────┘
```

## Runtime pieces

| Piece | Technology | Notes |
|---|---|---|
| API host | .NET 10, ASP.NET Core minimal APIs | composes every module from `Hosts/Api/Modules.cs`; owns authentication, authorization, antiforgery, rate limiting, ProblemDetails, OpenAPI, health |
| Modules | one assembly + one contracts assembly each | own schema, route group, feature flag, permissions; communicate through contracts and integration events |
| Web app | Next.js 16 App Router | routing-only `app/`, feature folders mirroring modules, Server Components and Server Functions, nonce CSP |
| Database | SQL Server (owner's instance) / Azure SQL | one database, schema per module, migrations per module, applied by the migrator container in production |
| Configuration | Azure App Configuration + Key Vault | one store, environment labels, secrets as Key Vault references |
| Identity | Microsoft Entra ID | BFF cookie session issued by the API; permissions stored by the identity module |
| Observability | OpenTelemetry | OTLP exporter when configured; Aspire dashboard locally |
| AI | Microsoft.Extensions.AI | one `IChatClient` pipeline; per-module `Ai/` folders; every capability behind a feature flag |

## Request flow

1. The browser calls the web origin. Static assets and pages are served by Next.js; `/api/*` is routed to the API (the `proxy.ts` runtime rewrite, or the edge proxy in production).
2. The API validates the authentication cookie, the antiforgery token on state changes, and the caller's permissions, then dispatches to the module endpoint group.
3. Module endpoints bind the request, call a command or query handler, and map the `Result` to a typed HTTP result. Handlers use the module's DbContext; cross-module data comes from contracts.
4. Domain events raised inside a transaction become integration events in the outbox, delivered in-process to consuming modules.

## Repository layout

See the repository README for the top-level map and `module-anatomy.md` for the inside of a module.
