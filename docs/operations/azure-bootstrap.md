# Azure bootstrap (operator guide)

The scripts under `scripts/azure/` create and converge the only Azure resources this project uses: one App Configuration store, one Key Vault per environment and, from sub-phase `azure-configuration-entra-app-registration-scripts`, the Entra app registrations. Every script is idempotent: a second run with the same parameter file changes nothing, with one deliberate exception: `seed.sh` bumps the labelled `Erp:Sentinel` on every run unless `--no-sentinel` is given (section 5b). Nothing in this guide or in the scripts deletes or purges a resource.

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
| openssl | on `PATH`; `export-runtime-certificate.sh` checks the exported PEM with it |
| Node.js | 24 LTS (`node` parses deployment output; `jq` is never required) |
| Subscription rights | Owner, or Contributor plus User Access Administrator, on the subscription or on the resource group once it exists |
| Entra rights | Application Administrator or Cloud Application Administrator for `entra.sh`; Global Administrator only when the tenant refuses the delegated permission grant and the admin-consent fallback in section 5a is needed |

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

Run every command from the repository root.

| Step | Command | What it does |
|---|---|---|
| 1 | `bash scripts/azure/check.sh` | compiles the Bicep template with the linter and syntax-checks every script; no sign-in needed |
| 2 | `bash scripts/azure/provision.sh --dry-run` | prints the `what-if` result and every data-plane command without writing anything |
| 3 | `bash scripts/azure/provision.sh` | creates or converges the resource group, the store, both vaults, the data-protection keys, the role assignments and the two labelled `Erp:Sentinel` keys |
| 4 | `bash scripts/azure/entra.sh --dry-run` | prints every Graph, Key Vault and App Configuration write it would make, with `<pending>` in place of the ids a create would produce; writes nothing |
| 5 | `bash scripts/azure/entra.sh` | creates or converges the three app registrations, their service principals, app roles, Graph consent, certificates and key credentials, assigns the operator `Erp.Admin` and writes the identity ids to App Configuration (section 5a) |
| 6 | `bash scripts/azure/provision.sh` | run again: grants the runtime service principal App Configuration Data Reader on the store and Key Vault Secrets User plus Crypto User on the production vault, which cannot exist before step 5 |
| 7 | `bash scripts/azure/seed.sh --dry-run` | previews every import with the CLI's own `--dry-run` and prints every other write; writes nothing |
| 8 | `bash scripts/azure/seed.sh` | imports `infra/appconfig` (unlabelled defaults, each label file, feature flags, Key Vault references) and bumps the labelled sentinels (section 5b) |
| 9 | `bash scripts/azure/verify.sh` | reads the provisioned resources back and prints `[ok]` or `[FAIL]` per assertion |
| 10 | `bash scripts/azure/verify.sh --entra` | reads the registrations, service principals, grants, certificates, key credentials and identity ids back |
| 11 | `bash scripts/azure/verify.sh --labels` | reads every seeded value, flag and reference back and proves the `local-dev` label overrides the unlabelled default |

`provision.sh` ends by printing `APPCONFIG_ENDPOINT=https://<store>.azconfig.io` and both vault URIs. Keep the endpoint: developers put it in `dotnet user-secrets`, and the server puts it in `infra/compose/.env`, from which `compose.yaml` passes it to the api container as `APPCONFIG_ENDPOINT`.

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
| `entra.sh` | the same sign-in as Application Administrator or Cloud Application Administrator (Global Administrator only for the admin-consent fallback in section 5a), plus the operator's Key Vault Certificates Officer role on both vaults and App Configuration Data Owner on the store |
| `export-runtime-certificate.sh` | the operator's Key Vault Secrets Officer role on the production vault |
| `seed.sh` | the operator's roles on the store and both vaults |
| `verify.sh` | read access to the resource group, App Configuration Data Reader or Owner on the store, and key read rights on both vaults (the operator's roles cover this); `--entra` additionally needs directory read access and certificate read rights on both vaults |

No script signs in or switches subscriptions itself. When the CLI context differs from `params.env`, the script stops and prints the exact `az login --tenant <id>` or `az account set --subscription <id>` command to run.

## 5a. Entra app registrations

`bash scripts/azure/entra.sh` creates or converges three single-tenant app registrations. Each one is found by its exact display name from `params.env`: no match creates it, one match converges it property by property, more than one match stops the script and names the duplicates. A second run with the same parameters performs no Microsoft Graph or Key Vault write. No registration ever holds a client secret; every credential is a certificate whose private key lives only in Key Vault (and, for the runtime registration, in the server's compose secret file).

| Registration | Parameter | Platform | Used by |
|---|---|---|---|
| local-dev sign-in | `ERP_AZURE_APP_WEB_LOCAL_DEV_NAME` | web | the API on a developer machine, which signs employees in on the browser's behalf (the browser holds only a cookie) |
| production sign-in | `ERP_AZURE_APP_WEB_PRODUCTION_NAME` | web | the deployed API, in the same way |
| runtime | `ERP_AZURE_APP_RUNTIME_NAME` | none | the service principal the api container signs in as to read the store and the production vault; no redirect URIs, no app roles, no Graph permissions |

Properties every registration converges to: `signInAudience` `AzureADMyOrg`; implicit grant off (`enableIdTokenIssuance` and `enableAccessTokenIssuance` both `false`); `api.requestedAccessTokenVersion` `2`; no single-page, mobile or fallback public client platform. The operator is added as owner of every registration (Microsoft Graph does not record the creator as owner); a same-named registration owned by someone else is refused, never adopted. Sign-in registrations additionally get a service principal with `appRoleAssignmentRequired` `true`, so only assigned users can sign in.

### Redirect URIs

| Registration | Redirect URIs |
|---|---|
| local-dev sign-in | `<ERP_AZURE_LOCAL_WEB_ORIGIN>/api/auth/signin-oidc`, `<ERP_AZURE_LOCAL_WEB_ORIGIN>/api/auth/signout-callback-oidc`, `<ERP_AZURE_LOCAL_API_ORIGIN>/api/auth/signin-oidc`, `<ERP_AZURE_LOCAL_API_ORIGIN>/api/auth/signout-callback-oidc` |
| production sign-in | `<ERP_AZURE_PRODUCTION_WEB_ORIGIN>/api/auth/signin-oidc`, `<ERP_AZURE_PRODUCTION_WEB_ORIGIN>/api/auth/signout-callback-oidc`; while the parameter is empty no redirect URI is requested and the script warns that production sign-in stays impossible until the parameter is set and `entra.sh` is run again. A re-run with an empty parameter never removes redirect URIs that are already registered: it stops and lists them, and only `bash scripts/azure/entra.sh --clear-production-redirect-uris` removes them deliberately |
| runtime | none |

### App roles

The roles live in `scripts/azure/entra/app-roles.json` with fixed ids, so the same ids exist in every tenant the scripts are ever run against.

| Value | Display name | Id | Purpose |
|---|---|---|---|
| `Erp.User` | ERP User | `3f1c9a6e-2b4d-4e8f-9c1a-5d7e8f0a1b2c` | signed-in employee with the baseline permissions |
| `Erp.Admin` | ERP Administrator | `a8d2e4f6-1c3b-4a5d-8e9f-0b1c2d3e4f5a` | administers the ERP and holds every permission |

Both roles are assignable to users only and enabled. `entra.sh` updates the roles only when the registration's roles differ from the file (compared on id, value, enabled state, display name, description and member types). A role that is enabled on the registration but absent from the file is never removed: Entra requires it to be disabled first, so the script warns and skips the role update. The operator who runs the script is assigned `Erp.Admin` on both sign-in service principals; further assignments are made in the Entra portal or with `az rest` against `appRoleAssignedTo`.

### Consent path

Each sign-in registration requests the Microsoft Graph delegated permissions `openid`, `profile` and `User.Read`. Their permission ids are read from the Microsoft Graph service principal at run time, never hard-coded. `entra.sh` then grants them for the whole tenant with `az ad app permission grant --scope "openid profile User.Read"` as the operator, so no employee sees a consent prompt. Only the tenant-wide grant counts: a per-user consent recorded by an interactive sign-in never satisfies the check, and a tenant-wide grant whose scopes differ from exactly these three is re-issued. When the tenant's consent policy refuses that grant, the script prints the exact command for a Global Administrator to run:

```bash
az ad app permission admin-consent --id <application id>
```

The script still finishes every other step for every registration, then exits 1 so the refusal is not missed. Run the printed command, then run `entra.sh` again; it will find the grant in place and report it unchanged.

### Certificates

| Registration | Vault | Certificate and secret name | Content type | Subject |
|---|---|---|---|---|
| local-dev sign-in | `ERP_AZURE_KEYVAULT_DEV_NAME` | `Erp--Platform--Identity--ClientCertificate` | `application/x-pkcs12` | `CN=erp-ai-pro-web-local-dev` |
| production sign-in | `ERP_AZURE_KEYVAULT_PROD_NAME` | `Erp--Platform--Identity--ClientCertificate` | `application/x-pkcs12` | `CN=erp-ai-pro-web-production` |
| runtime | `ERP_AZURE_KEYVAULT_PROD_NAME` | `Erp--Platform--Identity--RuntimeClientCertificate` | `application/x-pem-file` | `CN=erp-ai-pro-runtime-production` |

Every certificate is self-signed by Key Vault from the policy in `scripts/azure/entra/certificate-policy.json`: RSA 2048, exportable, key usage `digitalSignature` and `keyEncipherment`, valid for 12 months, and deliberately without auto-renewal. Auto-renewal would create a new key in the vault that the registration never learns about, so rotation is an explicit loop the operator runs:

1. `bash scripts/azure/entra.sh --rotate-signin-certificate local-dev|production` or `bash scripts/azure/entra.sh --rotate-runtime-certificate` creates a new certificate version with the same policy and appends its public key to the registration as a further key credential (`az ad app credential reset --keyvault --cert --append`; the private key never leaves the vault). The sign-in variant prints the `Erp:Sentinel` bump command for that label so the API picks the new version up; the runtime variant prints the export step below.
2. Once every API instance uses the new version, `bash scripts/azure/entra.sh --prune-old-credentials local-dev|production|runtime` deletes every key credential whose thumbprint differs from the current vault version. It refuses to run when the current version is not among the registration's credentials.

`verify.sh --entra` prints a `[warn]` line, not a failure, for a certificate that expires within 30 days. No script prints certificate, key or secret material.

### Runtime certificate export

The api container signs in as the runtime service principal with the PEM file from the production vault, placed on the server as a compose secret. The export is done by hand, once per certificate version:

1. `bash scripts/azure/export-runtime-certificate.sh --out <file>` (default `scripts/azure/out/erp-runtime-client.pem`; the folder and every `.pem` file are gitignored). The script refuses an existing path, accepts only a `.pem` destination that git ignores, downloads the current secret version with a restrictive umask and mode 600 (on Windows the file inherits the folder's permissions instead, so keep it inside your user profile), and checks it with `openssl x509 -noout -subject -enddate` and `openssl rsa -check -noout`; when either check fails the file is deleted and the script stops. It never echoes the content.
2. Copy the file to the server over an encrypted channel, never through chat, email or a ticket.
3. On the server, from the deployment checkout: `install -d -m 0700 infra/compose/secrets` once, then `install -o 1654 -g 1654 -m 0400 erp-runtime-client.pem infra/compose/secrets/`. 1654 is the uid and gid of the `app` user in the chiseled aspnet image the api container runs as (`APP_UID` in the image); `compose.production.yaml` mounts the file as the secret `erp-runtime-client.pem` at `/run/secrets/erp-runtime-client.pem`, and a file with any other owner is unreadable to the container.
4. Delete the local copy.

### Second provision.sh run

The runtime service principal does not exist until `entra.sh` has created it, so the role assignments for it are skipped on the first `provision.sh` run. Run `bash scripts/azure/provision.sh` again after `entra.sh`: it resolves the service principal by the display name in `ERP_AZURE_APP_RUNTIME_NAME` and grants App Configuration Data Reader on the store and Key Vault Secrets User plus Key Vault Crypto User on the production vault. `verify.sh --entra` reports `[FAIL] ... run provision.sh again` while those rows are missing.

### What entra.sh writes to App Configuration

| Key | Label | Value |
|---|---|---|
| `Erp:Platform:Identity:TenantId` | `local-dev` and `production` | `ERP_AZURE_TENANT_ID` |
| `Erp:Platform:Identity:ClientId` | `local-dev` | application (client) id of the local-dev sign-in registration |
| `Erp:Platform:Identity:ClientId` | `production` | application (client) id of the production sign-in registration |

Nothing else: `Erp:Platform:Identity:Instance` and the Key Vault reference `Erp:Platform:Identity:ClientCertificate` belong to `seed.sh` (section 5b), and the runtime registration is never written to App Configuration. In particular the runtime certificate is never an App Configuration Key Vault reference: the container must already hold it to reach the store at all, so it travels only as the compose secret file above.

## 5b. Seeding the store

`bash scripts/azure/seed.sh` is the only writer of the keys in `infra/appconfig/` and never deletes a key. Per run it validates the seed files (`node scripts/azure/lib/seed-files.ts validate`), imports `defaults.json` unlabelled and each label file under its label with `az appconfig kv import --format json --separator : --import-mode ignore-match` (only a changed or missing value is written), creates each feature flag of `feature-flags.json` under each label and enables or disables it to match the file, writes each Key Vault reference of `key-vault-references.json` after confirming the secret exists in that label's vault (`kv-erp-ai-pro-dev` for `local-dev`, `kv-erp-ai-pro-prod` for `production`), bumps the labelled `Erp:Sentinel` and prints a read-back table per label. Options: `--label local-dev|production|all` (default `all`), `--no-sentinel` (seed without triggering a reload), `--dry-run`.

- A missing secret stops the run with the vault and secret name: create the secret first (the sign-in certificates come from `entra.sh`); the script never writes a placeholder, because an unresolvable reference fails API startup and a placeholder would later be read as the real value.
- The sentinel bump is the only write an unchanged re-run performs; a running API reloads within `Erp:Platform:Configuration:RefreshInterval`, and restarting the api container applies the change at once.
- How to add a setting, a flag or a secret: [docs/guides/adding-a-setting.md](../guides/adding-a-setting.md).

## 6. Developer onboarding

1. Ask the operator to add you to the developers group when `ERP_AZURE_DEVELOPERS_GROUP` is configured; otherwise the operator assigns App Configuration Data Reader on the store and Key Vault Secrets User plus Key Vault Crypto User on the development vault to your account by hand.
2. `az login --tenant <tenant id>` on your machine.
3. Store the endpoint the operator gives you: `cd backend && dotnet user-secrets set APPCONFIG_ENDPOINT https://<store>.azconfig.io --project Hosts/Api/Dewiride.Erp.Host.Api`. The next `dotnet run` loads the store with the `local-dev` label and its Key Vault references; without the value the API runs on `appsettings.json` plus user secrets.
4. A new role assignment can take up to 15 minutes to propagate; a `403` from the store or the vault inside that window is expected.

## 7. Production server

The server signs in as the runtime service principal with the certificate exported in section 5a; it never holds a client secret and never reaches the vault through a browser or a developer account. The api container needs these values; `compose.yaml` and `compose.production.yaml` carry the variables (interpolated from `infra/compose/.env`) and mount the certificate as the compose secret `erp-runtime-client.pem`, and the API validates every one of them at startup before its first call to Azure:

| Variable | Value | Source |
|---|---|---|
| `AZURE_TENANT_ID` | the tenant id | `ERP_AZURE_TENANT_ID` in `params.env`; also printed by `entra.sh` |
| `AZURE_CLIENT_ID` | the runtime registration's application (client) id | printed by `entra.sh` as `AZURE_CLIENT_ID=<id>` |
| `AZURE_TOKEN_CREDENTIALS` | `EnvironmentCredential` | fixed; restricts Azure.Identity to the certificate credential below |
| `AZURE_CLIENT_CERTIFICATE_PATH` | `/run/secrets/erp-runtime-client.pem` | the compose secret mounted from `infra/compose/secrets/erp-runtime-client.pem` |
| `APPCONFIG_ENDPOINT` | `https://<store>.azconfig.io` | printed by `provision.sh` |

`ERP_ENVIRONMENT=production` selects the label. The placement of the certificate on the host is described in section 5a; the first deployment that uses these values belongs to the `first-deployment` phase.

## 8. Re-running and recovery

- Re-running is the normal way to converge: every run performs the same exists-and-diff checks, and a run that changes nothing exits 0 with `unchanged` in its log lines.
- `--dry-run` writes nothing: control-plane changes are shown through `az deployment group what-if`, and every data-plane command is printed with a `[dry-run]` prefix instead of being executed. Before the resource group exists the what-if step is skipped, because what-if needs a scope to run against.
- What-if noise: on a converged deployment what-if still reports `Modify` lines for properties Azure fills in or does not echo back: on the store `dataPlaneProxy`, `defaultKeyValueRevisionRetentionPeriodInSeconds` and, on the Free tier, `softDeleteRetentionInDays: 0 => 7`; on each vault `networkAcls` shown as added; on each key `rotationPolicy` shown as removed; on each role assignment `principalType` as `NoEffect`. None of these is applied: a second run leaves the store settings, the key versions and the rotation policies exactly as they were. A run is converged when what-if shows no `Create` or `Delete` entry and no `Modify` beyond this list.
- Soft-deleted vault: `az keyvault recover --name <vault>` and then run `provision.sh` again, because recovery restores the vault and its keys but drops the role assignments.
- Soft-deleted certificate (deleted in the portal to start over): `entra.sh` stops and prints `az keyvault certificate recover --vault-name <vault> --name <certificate>`; purge protection keeps the name reserved, so recovery is the only way forward.
- Store on the Free tier: there is no soft delete, so a deleted store is recreated by `provision.sh` and repopulated by `seed.sh` (and `entra.sh`, which writes the identity ids). On `Standard` or `Premium` run `az appconfig recover --name <store>` first.
- `NameUnavailable` on the store or `VaultAlreadyExists` on a vault means the name is taken globally, either by another subscription or by a soft-deleted vault of your own (`az keyvault list-deleted`). Recover your own vault as above or choose a new name in `params.env`.
- A `403` immediately after a fresh role assignment is propagation delay: the scripts retry data-plane calls for about five minutes (20 attempts, 15 seconds apart) and then stop with a message that role assignments can take up to 15 minutes to propagate; re-run when it does.

## 9. Never automated

- Deleting or purging the resource group, the store, a vault, a key, a secret or a certificate is never scripted. Deletion is a deliberate owner action in the portal or with the CLI, and purge protection makes a purged vault impossible before the retention period ends.
- A vault name stays reserved for the 90-day soft-delete retention after deletion; plan renames accordingly.
