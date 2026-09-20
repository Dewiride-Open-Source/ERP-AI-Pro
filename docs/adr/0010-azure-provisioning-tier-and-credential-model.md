# ADR-0010: Azure provisioning, App Configuration tier and operational credential model

Status: accepted
Date: 2026-09-20

## Context

ADR-0005 limits Azure use to App Configuration, Key Vault, Microsoft Entra ID and, in production, Azure SQL, and fixes the configuration model: one App Configuration store with the labels `local-dev` and `production`, one Key Vault per environment reached only through Key Vault references, and Microsoft Entra ID for every credential. The `azure-configuration` phase has to create those resources, and the owner set the constraints: a fresh resource group (the older ERP resources in the subscription stay untouched), bash scripts only, no tool beyond the Azure CLI and what Git Bash and Ubuntu already ship, every state-changing command run with the owner's consent, and the lowest-cost App Configuration tier that still supports the refresh model. The CI federated credential named in ADR-0005 has no consumer yet: no workflow needs Azure.

## Decision

- Control plane: a Bicep template under `scripts/azure/bicep/` (resource group scope) declares the store, both vaults, the data-protection key `Erp--Platform--DataProtection--Key` in each vault and every role assignment. `provision.sh` deploys it with `az deployment group create --mode Incremental`; `--dry-run` runs `az deployment group what-if` and prints the data-plane commands instead of executing them. Role assignments are named `guid(resourceId, principalId, roleDefinitionId)` so a repeated deployment converges to the same resources instead of creating duplicates.
- Data plane and Entra: bash with `az` and `az rest`, every write preceded by an exists-and-diff check so a second run performs no write. Scripts live in `scripts/azure/`, read a gitignored `params.env` line by line and never `source` it. Committed files carry no tenant or subscription id and no identifier of a provisioned resource beyond the default names in `params.env.example`, which the gitignored `params.env` may override.
- App Configuration tier: `Free`, by the owner's cost decision. The API therefore checks the sentinel and feature flags every 30 minutes (about three reads per check), the store has no soft delete, the seed files under `infra/appconfig/` are the recovery path for its content, and the interval is a setting so it can be shortened after an in-place upgrade to `Standard`, which the template applies with purge protection when the SKU parameter changes.
- Key Vault: one vault per environment with RBAC authorization, soft delete (90 days) and purge protection; local authentication on the store is disabled.
- Access model: the operator holds App Configuration Data Owner on the store and Key Vault Secrets Officer, Certificates Officer and Crypto Officer on both vaults; an optional developers group holds App Configuration Data Reader and, on the development vault, Key Vault Secrets User and Crypto User; the runtime service principal holds App Configuration Data Reader and, on the production vault, Key Vault Secrets User and Crypto User. Every assignment is scoped to the individual resource.
- Credentials: code uses `DefaultAzureCredential` only. Developer machines set `AZURE_TOKEN_CREDENTIALS=dev` and rely on `az login`. The server sets `AZURE_TOKEN_CREDENTIALS=EnvironmentCredential` with `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` and `AZURE_CLIENT_CERTIFICATE_PATH` pointing at a compose secret file; certificates only, no client secret on any registration. One sign-in registration exists per environment plus one runtime service principal for production. The sign-in certificates reach the API as App Configuration Key Vault references; the runtime certificate is exported from the production vault by hand and placed on the server, never referenced from the store.
- The CI federated credential named in ADR-0005 is created by the sub-phase whose workflow first needs Azure, not before.

## Consequences

- Request budget: the live system spends about 150 App Configuration reads per day, well inside the Free tier's 1,000 per day even with developer sessions running against the same store.
- A settings change reaches the API within 30 minutes, or immediately when the api container is restarted.
- Recovery of the store is a re-run of `provision.sh` and `seed.sh`; recovery of a soft-deleted vault is `az keyvault recover` followed by `provision.sh`, because recovery drops role assignments.
- The `authentication` phase wires ASP.NET Core Data Protection to `Erp--Platform--DataProtection--Key`; the `first-deployment` phase places the runtime certificate on the server.
- The sub-phase that first needs Azure from GitHub Actions revisits the CI credential and records the federated credential subject it creates.
