# ADR-0005: Configuration and secrets model

Status: accepted
Date: 2026-09-19

## Context

The owner allows exactly these Azure services: Azure App Configuration, Azure Key Vault, Microsoft Entra ID and, in production, Azure SQL. Everything else runs locally or on the home server. Two environments exist: `local-dev` (developer machines and CI) and `production` (Ubuntu server). Secrets must never enter the repository, compose files or images.

## Decision

- One App Configuration store holds all non-secret settings. Values without a label are defaults; the label `local-dev` or `production` overrides them. `ERP_ENVIRONMENT` selects the label; `ASPNETCORE_ENVIRONMENT` keeps its framework meaning.
- Secrets live in one Key Vault per environment and reach the application only as App Configuration Key Vault references, resolved by the provider with `DefaultAzureCredential`.
- Configuration load order: `appsettings.json` → user secrets (Development only) → environment variables → key-per-file `/run/secrets` → App Configuration (unlabelled, then labelled) → Key Vault references. The App Configuration provider is added only when `APPCONFIG_ENDPOINT` is set; in `Production` its absence fails startup; tests never set it.
- Key naming: `Erp:<Domain>:<Module>:<Setting>`; sentinel `Erp:Sentinel` triggers a full refresh; feature flags `Erp.Modules.<Domain>.<Module>[.<Capability>]` via Microsoft.FeatureManagement (dots, because colons are forbidden in flag names); Key Vault secrets `Erp--<Domain>--<Module>--<Name>`.
- Feature code binds typed options (`AddOptions<T>().BindConfiguration().ValidateDataAnnotations().ValidateOnStart()`) and never reads `IConfiguration` directly.
- Credentials to Azure: `az login` on developer machines; a GitHub OIDC federated credential in CI; a certificate credential from a compose secret file on the server. One app registration per environment.
- Every configuration key, feature flag and secret name is registered in `docs/configuration.md`.

## Consequences

- The App Configuration tier is chosen in the Azure configuration phase: the free tier's 1,000 requests per day cannot sustain a one-minute sentinel poll, so either the refresh interval is five minutes or more, or the Developer/Standard tier is used.
- Rotating a secret is a Key Vault operation followed by a sentinel bump; no image rebuild.
- The database connection string for local development (the owner's SQL Server instance) is a Key Vault secret in the local-dev vault.
