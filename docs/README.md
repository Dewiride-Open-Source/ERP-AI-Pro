# Documentation index

| Section | Contents |
|---|---|
| [Roadmap](roadmap/ROADMAP.md) | generated view of `roadmap/roadmap.json`, the single source of truth for what is planned, in progress and done |
| [Architecture](architecture/overview.md) | [overview](architecture/overview.md), [module anatomy](architecture/module-anatomy.md), [dependency rules](architecture/dependency-rules.md), [naming and namespaces](architecture/naming-and-namespaces.md), [adding a module](architecture/adding-a-module.md), [persistence](architecture/persistence.md), [application pipeline](architecture/application-pipeline.md), [HTTP conventions](architecture/http-conventions.md), [request pipeline](architecture/request-pipeline.md), [attachments](architecture/attachments.md), [design system](architecture/design-system.md), [AI capabilities](architecture/ai-capabilities.md) |
| [Guides](guides/local-development.md) | [local development](guides/local-development.md), [testing](guides/testing.md), [adding a setting, flag or secret](guides/adding-a-setting.md), [comment policy](guides/comment-policy.md), [observability](guides/observability.md), [migrations and seeding](guides/migrations.md) |
| [Operations](operations/github-repository-settings.md) | [GitHub repository settings](operations/github-repository-settings.md), [Azure bootstrap](operations/azure-bootstrap.md), [secrets runbook](operations/runbooks/secrets.md), [migrations runbook](operations/runbooks/migrations.md); the other deployment runbooks arrive with the first-deployment phase |
| [Contributor licence agreements](cla/sign-cla.md) | how outside contributors sign, and the [individual](cla/individual-cla-1.0.md) and [corporate](cla/corporate-cla-1.0.md) agreements |
| [Configuration register](configuration.md) | every configuration key, feature flag and secret name |
| [Routing register](routing.md) | every API route group and web route |
| [OpenAPI document](openapi/erp.json) | the committed contract of the API, refreshed by `OpenApiSnapshotTests` and the source of the generated web client |

## Architecture decision records

| ADR | Title |
|---|---|
| [0001](adr/0001-record-architecture-decisions.md) | Record architecture decisions |
| [0002](adr/0002-modular-monolith-module-anatomy.md) | Modular monolith with four projects per module |
| [0003](adr/0003-dotnet-10-slnx-cpm-lock-files.md) | .NET 10 LTS, XML solution format, central package management and host lock files |
| [0004](adr/0004-bff-cookie-auth-entra-id.md) | Backend-for-frontend cookie authentication with Microsoft Entra ID |
| [0005](adr/0005-configuration-and-secrets.md) | Configuration and secrets model |
| [0006](adr/0006-testing-platform-xunit-v3.md) | Microsoft.Testing.Platform with xUnit v3 |
| [0007](adr/0007-nextjs-app-router-feature-folders.md) | Next.js App Router with feature folders that mirror the backend |
| [0008](adr/0008-local-sql-server-and-azure-sql.md) | Owner's SQL Server for development, Azure SQL in production, one schema per module |
| [0009](adr/0009-third-party-packages.md) | Third-party package approvals and bans |
| [0010](adr/0010-azure-provisioning-tier-and-credential-model.md) | Azure provisioning, App Configuration tier and operational credential model |
| [0011](adr/0011-configuration-source-selection-and-runtime-credential.md) | Configuration source selection and runtime credential |
| [0012](adr/0012-feature-flag-catalog-and-defaults.md) | Feature flag catalog, default-enabled modules and the disabled-module contract |
| [0013](adr/0013-persistence-conventions-and-test-databases.md) | Persistence conventions, per-process test databases and the CI SQL Server action |
| [0014](adr/0014-cn-class-name-helper.md) | The `cn` package replaces `clsx` and `tailwind-merge` |
| [0015](adr/0015-auditing-soft-delete-and-domain-primitives.md) | Auditing, soft delete, row versions, the actor model and the domain primitives |
| [0016](adr/0016-application-pipeline-and-idempotency.md) | The application pipeline, the unit of work, query contracts and idempotency keys |
| [0017](adr/0017-http-conventions-openapi-snapshot-and-generated-client.md) | HTTP conventions, the committed OpenAPI document and the generated TypeScript client |
| [0018](adr/0018-transport-security-and-the-request-pipeline-order.md) | Transport security and the request pipeline order |
| [0019](adr/0019-rate-limiting-reference-data-caching-and-health.md) | Rate limiting, reference-data caching and health checks |
| [0020](adr/0020-observability-baseline.md) | Observability baseline |
| [0021](adr/0021-migrator-program-and-seeding.md) | The migrator program, migration tooling and seeding |
| [0022](adr/0022-attachments-in-azure-blob-storage.md) | Attachments in Azure Blob Storage behind an application encryption envelope |
| [0023](adr/0023-design-tokens-shared-theme-and-generated-primitives.md) | Design tokens, the shared theme stylesheet and generated primitives |
| [0024](adr/0024-frontend-structure-and-import-boundaries.md) | Frontend structure and import boundaries |
| [0025](adr/0025-forms-typed-submissions-and-form-composites.md) | Forms: typed submissions, one schema, field-error mapping and the form composites |
| [0026](adr/0026-server-driven-lists-and-the-data-table.md) | Server-driven lists: the view in the address and the data table |
| [0027](adr/0027-feedback-motion-and-the-accessibility-baseline.md) | Feedback, motion and the accessibility baseline |
| [0028](adr/0028-playwright-matrix-navigation-crawl-and-sharded-reports.md) | Playwright matrix, navigation crawl and sharded reports |
| [0029](adr/0029-captures-of-a-page-at-rest-and-the-hydration-marker.md) | Captures of a page at rest and the hydration marker |
| [0030](adr/0030-community-and-enterprise-editions.md) | Community and Enterprise editions, the LGPL-3.0-only licence and the contributor licence agreement |
| [0031](adr/0031-sign-in-through-entra-in-the-api.md) | Sign-in through Microsoft Entra ID in the API |
| [0032](adr/0032-token-cache-in-sql-server-and-the-data-protection-key-ring.md) | The token cache in SQL Server and the Data Protection key ring |
| [0033](adr/0033-antiforgery-for-requests-the-session-cookie-signs-in.md) | Antiforgery for requests the session cookie signs in |
| [0034](adr/0034-session-endpoints-bearer-tokens-per-route-and-the-security-description.md) | Session endpoints, bearer tokens per route and the security description |
| [0035](adr/0035-signed-in-browser-tests-through-an-end-to-end-api-host.md) | Signed-in browser tests through an end-to-end API host |
| [0036](adr/0036-web-sign-in-the-session-in-the-browser-and-single-sign-out.md) | Web sign-in, the session in the browser and single sign-out |

New decisions use [0000-template.md](adr/0000-template.md).
