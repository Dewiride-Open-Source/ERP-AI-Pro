# Azure bootstrap (operator guide)

The scripts under `scripts/azure/` create and converge the only Azure resources this project uses: one App Configuration store, one Key Vault per environment and, from sub-phase `azure-configuration-entra-app-registration-scripts`, the Entra app registrations. Every script is idempotent: a second run with the same parameter file changes nothing. Nothing in this guide or in the scripts deletes or purges a resource.

## 1. Purpose and resources

Resource names come from `scripts/azure/params.env`; the defaults below are the values in `scripts/azure/params.env.example`.

| Resource | Default name | Purpose |
|---|---|---|
| Resource group | `rg-erp-ai-pro` | holds everything below; the group, the store and both vaults are tagged `project=erp-ai-pro`, `managedBy=scripts/azure` |
| App Configuration store | `appcs-erp-ai-pro` | every non-secret setting and feature flag, unlabelled defaults overridden by the labels `local-dev` and `production`; local authentication disabled, Entra ID only |
| Key Vault (development) | `kv-erp-ai-pro-dev` | secrets and certificates for `local-dev`, reached by the API only as App Configuration Key Vault references |
| Key Vault (production) | `kv-erp-ai-pro-prod` | secrets and certificates for `production`, including the runtime certificate the server signs in with |
| Data-protection key | `Erp--Platform--DataProtection--Key` in both vaults | RSA 2048 key with `wrapKey`/`unwrapKey`; the `authentication` phase protects the ASP.NET Core Data Protection key ring with it |

Both vaults use RBAC authorization, soft delete with a 90-day retention and purge protection. The store runs on the Free tier by the owner's decision (ADR-0010); it has no soft delete, so the seed files in the repository are its recovery path.

## 2. Prerequisites

| Requirement | Detail |
|---|---|
| Azure CLI with Bicep | current release; `az bicep install` once, `az bicep version` reports 0.30 or later |
| Shell | Git Bash on Windows or bash on Ubuntu; the scripts set `MSYS_NO_PATHCONV=1` themselves |
| openssl | on `PATH` (certificate checks in the app-registration sub-phase) |
| Node.js | 24 LTS (`node` parses deployment output; `jq` is never required) |
| Subscription rights | Owner, or Contributor plus User Access Administrator, on the subscription or on the resource group once it exists |
| Entra rights | Application Administrator or Cloud Application Administrator, needed from sub-phase `azure-configuration-entra-app-registration-scripts` |

## 3. Parameter file

```bash
cp scripts/azure/params.env.example scripts/azure/params.env
```

`params.env` is gitignored. The scripts read it line by line (`KEY=VALUE`, `#` comments, optional surrounding quotes) and never `source` it. The `ERP_AZURE_` prefix keeps these values apart from the `AZURE_*` variables that Azure.Identity reads.

| Parameter | Required | Default | Meaning |
|---|---|---|---|
| `ERP_AZURE_TENANT_ID` | yes | — | Entra tenant id; `az account show` must report the same tenant |
| `ERP_AZURE_SUBSCRIPTION_ID` | yes | — | subscription id; `az account show` must report the same subscription |
| `ERP_AZURE_LOCATION` | yes | `centralindia` | Azure region of the resource group and every resource in it |
| `ERP_AZURE_RESOURCE_GROUP` | yes | `rg-erp-ai-pro` | resource group name |
| `ERP_AZURE_APPCONFIG_NAME` | yes | `appcs-erp-ai-pro` | App Configuration store name (globally unique; the endpoint becomes `https://<name>.azconfig.io`) |
| `ERP_AZURE_KEYVAULT_DEV_NAME` | yes | `kv-erp-ai-pro-dev` | development vault name (globally unique, 3–24 characters) |
| `ERP_AZURE_KEYVAULT_PROD_NAME` | yes | `kv-erp-ai-pro-prod` | production vault name (globally unique, 3–24 characters) |
| `ERP_AZURE_APPCONFIG_SKU` | no | `Free` | `Free`, `Developer`, `Standard` or `Premium`; purge protection is applied only for `Standard` and `Premium` |
| `ERP_AZURE_DEVELOPERS_GROUP` | no | empty | display name or object id of an Entra security group whose members read the store and the development vault; empty skips those role assignments |
| `ERP_AZURE_PRODUCTION_WEB_ORIGIN` | no | empty | public origin of the production web app (`https://erp.example.com`); empty until the host name exists |
| `ERP_AZURE_LOCAL_WEB_ORIGIN` | no | `http://localhost:3000` | origin of the local web app |
| `ERP_AZURE_LOCAL_API_ORIGIN` | no | `http://localhost:5080` | origin of the local API |
| `ERP_AZURE_APP_WEB_LOCAL_DEV_NAME` | no | `ERP-AI-Pro Web (local-dev)` | display name of the local-dev sign-in registration |
| `ERP_AZURE_APP_WEB_PRODUCTION_NAME` | no | `ERP-AI-Pro Web (production)` | display name of the production sign-in registration |
| `ERP_AZURE_APP_RUNTIME_NAME` | no | `ERP-AI-Pro Runtime (production)` | display name of the runtime registration whose service principal the server signs in as |

## 4. Order of operations

Run every command from the repository root. `check.sh`, `provision.sh` and `verify.sh` arrive with sub-phase `azure-configuration-idempotent-azure-provisioning-scripts`; `entra.sh` arrives with `azure-configuration-entra-app-registration-scripts`; `seed.sh` arrives with `azure-configuration-configuration-conventions-and-seed-data`.

| Step | Command | What it does |
|---|---|---|
| 1 | `bash scripts/azure/check.sh` | compiles the Bicep template with the linter and syntax-checks every script; no sign-in needed |
| 2 | `bash scripts/azure/provision.sh --dry-run` | prints the `what-if` result and every data-plane command without writing anything |
| 3 | `bash scripts/azure/provision.sh` | creates or converges the resource group, the store, both vaults, the data-protection keys, the role assignments and the two labelled `Erp:Sentinel` keys |
| 4 | `bash scripts/azure/entra.sh --dry-run` | prints the Entra changes it would make |
| 5 | `bash scripts/azure/entra.sh` | creates or converges the app registrations, service principals and certificates |
| 6 | `bash scripts/azure/provision.sh` | grants the runtime service principal its roles, which cannot exist before step 5 |
| 7 | `bash scripts/azure/seed.sh` | imports the seed files and Key Vault references and bumps the labelled sentinels |
| 8 | `bash scripts/azure/verify.sh` | reads everything back and prints `[ok]` or `[FAIL]` per assertion |

`provision.sh` ends by printing `APPCONFIG_ENDPOINT=https://<store>.azconfig.io` and both vault URIs. Keep the endpoint: developers put it in `dotnet user-secrets`, and the server puts it in `infra/compose/.env` once sub-phase `azure-configuration-api-configuration-bootstrap` wires `APPCONFIG_ENDPOINT` into the compose files.

## 5. Access model

Role assignments are scoped to the individual resource and named deterministically (`guid(resourceId, principalId, roleDefinitionId)`), so re-running the deployment never duplicates them.

| Principal | Resource | Role | Role definition id |
|---|---|---|---|
| operator (signed-in user) | store | App Configuration Data Owner | `5ae67dd6-50cb-40e7-96ff-dc2bfa4b606b` |
| developers group (optional) | store | App Configuration Data Reader | `516239f1-63e1-4d78-a4de-a74fb236a071` |
| runtime service principal (optional) | store | App Configuration Data Reader | `516239f1-63e1-4d78-a4de-a74fb236a071` |
| operator | development vault | Key Vault Secrets Officer | `b86a8fe4-44ce-4948-aee5-eccb2c155cd7` |
| operator | development vault | Key Vault Certificates Officer | `a4417e6f-fecd-4de8-b567-7b0420556985` |
| operator | development vault | Key Vault Crypto Officer | `14b46e9e-c2b7-41b4-b07b-48a6ebf60603` |
| developers group (optional) | development vault | Key Vault Secrets User | `4633458b-17de-408a-b874-0445c86b69e6` |
| developers group (optional) | development vault | Key Vault Crypto User | `12338af0-0e69-4776-bea7-57ae8d297424` |
| operator | production vault | Key Vault Secrets Officer | `b86a8fe4-44ce-4948-aee5-eccb2c155cd7` |
| operator | production vault | Key Vault Certificates Officer | `a4417e6f-fecd-4de8-b567-7b0420556985` |
| operator | production vault | Key Vault Crypto Officer | `14b46e9e-c2b7-41b4-b07b-48a6ebf60603` |
| runtime service principal (optional) | production vault | Key Vault Secrets User | `4633458b-17de-408a-b874-0445c86b69e6` |
| runtime service principal (optional) | production vault | Key Vault Crypto User | `12338af0-0e69-4776-bea7-57ae8d297424` |

Key Vault Certificate User is deliberately unassigned: the API reads sign-in certificates through the secret endpoint, which Key Vault Secrets User covers.

Sign-in each script needs:

| Script | Sign-in |
|---|---|
| `check.sh` | none |
| `provision.sh` | `az login --tenant <ERP_AZURE_TENANT_ID>` as the operator: the subscription rights above plus directory read access to resolve the developers group and the runtime service principal |
| `entra.sh` | the same sign-in with the Entra rights above |
| `seed.sh` | the operator's roles on the store and both vaults |
| `verify.sh` | read access to the resource group, App Configuration Data Reader or Owner on the store, and key read rights on both vaults (the operator's roles cover this) |

No script signs in or switches subscriptions itself. When the CLI context differs from `params.env`, the script stops and prints the exact `az login --tenant <id>` or `az account set --subscription <id>` command to run.

## 6. Developer onboarding

1. Ask the operator to add you to the developers group when `ERP_AZURE_DEVELOPERS_GROUP` is configured; otherwise the operator assigns App Configuration Data Reader on the store and Key Vault Secrets User plus Key Vault Crypto User on the development vault to your account by hand.
2. `az login --tenant <tenant id>` on your machine.
3. Store the endpoint the operator gives you: `cd backend && dotnet user-secrets set APPCONFIG_ENDPOINT https://<store>.azconfig.io --project Hosts/Api/Dewiride.Erp.Host.Api`. The API reads it from sub-phase `azure-configuration-api-configuration-bootstrap`; until then the value is stored but unused.
4. A new role assignment can take up to 15 minutes to propagate; a `403` from the store or the vault inside that window is expected.

## 7. Production server

The server signs in as the runtime service principal with a certificate exported from the production vault; the export procedure arrives with sub-phase `azure-configuration-entra-app-registration-scripts` and the placement on the host with the `first-deployment` phase.

## 8. Re-running and recovery

- Re-running is the normal way to converge: every run performs the same exists-and-diff checks, and a run that changes nothing exits 0 with `unchanged` in its log lines.
- `--dry-run` writes nothing: control-plane changes are shown through `az deployment group what-if`, and every data-plane command is printed with a `[dry-run]` prefix instead of being executed. Before the resource group exists the what-if step is skipped, because what-if needs a scope to run against.
- What-if noise: role assignments and Key Vault child resources can be reported as `Modify` or `NoEffect` on properties Azure fills in server-side (`principalType`, `createdOn`, `keyOps` ordering). A run whose what-if shows only `NoChange`, `Ignore` or `NoEffect` entries is converged.
- Soft-deleted vault: `az keyvault recover --name <vault>` and then run `provision.sh` again, because recovery restores the vault and its keys but drops the role assignments.
- Store on the Free tier: there is no soft delete, so a deleted store is recreated by `provision.sh` and repopulated by `seed.sh` (and `entra.sh`, which writes the identity ids). On `Standard` or `Premium` run `az appconfig recover --name <store>` first.
- `NameUnavailable` on the store or `VaultAlreadyExists` on a vault means the name is taken globally, either by another subscription or by a soft-deleted vault of your own (`az keyvault list-deleted`). Recover your own vault as above or choose a new name in `params.env`.
- A `403` immediately after a fresh role assignment is propagation delay: the scripts retry data-plane calls for about five minutes (20 attempts, 15 seconds apart) and then stop with a message that role assignments can take up to 15 minutes to propagate; re-run when it does.

## 9. Never automated

- Deleting or purging the resource group, the store, a vault, a key, a secret or a certificate is never scripted. Deletion is a deliberate owner action in the portal or with the CLI, and purge protection makes a purged vault impossible before the retention period ends.
- A vault name stays reserved for the 90-day soft-delete retention after deletion; plan renames accordingly.
