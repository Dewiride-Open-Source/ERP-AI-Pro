# Documentation index

| Section | Contents |
|---|---|
| [Roadmap](roadmap/ROADMAP.md) | generated view of `roadmap/roadmap.json`, the single source of truth for what is planned, in progress and done |
| [Architecture](architecture/overview.md) | [overview](architecture/overview.md), [module anatomy](architecture/module-anatomy.md), [dependency rules](architecture/dependency-rules.md), [naming and namespaces](architecture/naming-and-namespaces.md), [adding a module](architecture/adding-a-module.md), [persistence](architecture/persistence.md), [AI capabilities](architecture/ai-capabilities.md) |
| [Guides](guides/local-development.md) | [local development](guides/local-development.md), [testing](guides/testing.md), [adding a setting, flag or secret](guides/adding-a-setting.md), [comment policy](guides/comment-policy.md) |
| [Operations](operations/github-repository-settings.md) | [GitHub repository settings](operations/github-repository-settings.md), [Azure bootstrap](operations/azure-bootstrap.md), [secrets runbook](operations/runbooks/secrets.md); deployment runbooks arrive with the first-deployment phase |
| [Configuration register](configuration.md) | every configuration key, feature flag and secret name |
| [Routing register](routing.md) | every API route group and web route |

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

New decisions use [0000-template.md](adr/0000-template.md).
