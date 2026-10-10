# Architecture overview

ERP-AI-Pro is a modular monolith: one ASP.NET Core process hosts independent business modules behind one Next.js web application, deployed with Docker Compose on an on-premises Ubuntu server.

This repository is the Community edition, licensed LGPL-3.0-only. The Enterprise edition has its own hosts in the private repository `Dewiride-Enterprise/ERP-AI-Pro-Enterprise`, which compose the Community modules and additional proprietary modules through the Community host entry points; Enterprise depends on Community, never the reverse ([ADR-0030](../adr/0030-community-and-enterprise-editions.md), [dependency rules](dependency-rules.md#editions)).

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
                            │ standalone  │            Azure Blob Storage: attachments,
                            └─────────────┘            Entra only, encrypted by the API
```

## Runtime pieces

| Piece | Technology | Notes |
|---|---|---|
| API host | .NET 10, ASP.NET Core minimal APIs | composes every module from `Modules.All` in `Hosts/Composition`; owns authentication, authorization, antiforgery, rate limiting, ProblemDetails, OpenAPI, health |
| Modules | one assembly + one contracts assembly each | own schema, route group, feature flag, permissions; communicate through contracts and integration events |
| Web app | Next.js 16 App Router | routing-only `app/`, feature folders mirroring modules, Server Components and Server Functions, nonce CSP |
| Database | SQL Server (owner's instance) / Azure SQL | one database, schema per module, migrations per module, applied by the migrator container in production |
| Attachments | Azure Blob Storage (Azurite in tests, CI and the local containers) | `BuildingBlocks.Attachments` behind `IAttachmentService`: files encrypted by the API with an AES-256-GCM envelope, each file's data key wrapped by a key-encryption key kept as a Key Vault secret, before they reach one Entra-only storage account per environment, metadata in the `files` schema, routes in the `Platform/Attachments` module ([attachments](attachments.md), ADR-0022) |
| Configuration | Azure App Configuration + Key Vault | one store, environment labels, secrets as Key Vault references |
| Identity | Microsoft Entra ID | BFF cookie session issued by the API to a person whose record the module `Identity/Users` admits ([ADR-0039](../adr/0039-people-and-their-admission.md)); permissions stored by the identity modules from `user-management-roles-and-permissions` |
| Observability | OpenTelemetry | OTLP exporter when configured; Aspire dashboard locally |
| AI | Microsoft.Extensions.AI | the Community AI foundation: one `IChatClient` pipeline, the prompt library and tool registry, evaluation and safety; AI features are Enterprise modules, every capability behind a feature flag ([AI capabilities](ai-capabilities.md)) |

## Request flow

1. The browser calls the web origin. Static assets and pages are served by Next.js; `/api/*` is routed to the API (the `proxy.ts` runtime rewrite, or the edge proxy in production).
2. The API validates the authentication cookie, the antiforgery token on state changes, and the caller's permissions, then dispatches to the module endpoint group.
3. Module endpoints bind the request, call a command or query handler, and map the `Result` to a typed HTTP result. Handlers use the module's DbContext; cross-module data comes from contracts.
4. Domain events raised inside a transaction become integration events in the outbox, delivered in-process to consuming modules.
5. Files stream through the API in both directions; the browser never talks to storage. An upload is checked (name, type, content and size), hashed, encrypted and staged in Blob Storage, committed unless identical stored content can be verified to open, and recorded; a download is a short-lived link bound to the person who asked for it, then a decrypted stream whose redemption is recorded ([attachments](attachments.md)).

## Repository layout

See the repository README for the top-level map and `module-anatomy.md` for the inside of a module.
