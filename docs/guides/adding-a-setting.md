# Adding a setting, a feature flag or a secret

Every value the API reads comes from one of three places, and each place has one owner (ADR-0005, ADR-0011, and the owner's settings rule of 2026-09-29 recorded in ADR-0031):

| Place | Owner | What lives there |
|---|---|---|
| `backend/Hosts/Api/Dewiride.Erp.Host.Api/appsettings.json` | the code | the logging levels, the bootstrap-only `Erp:Platform:Configuration:*` intervals and the defaults of the host, rate-limiting and caching settings it already holds; never a new setting |
| `infra/appconfig/` (imported by `scripts/azure/seed.sh`) | the repository | every value the store holds: unlabelled defaults, per-label overrides, feature flags and Key Vault references |
| Key Vault (`kv-erp-ai-pro-dev`, `kv-erp-ai-pro-prod`) | the operator | secret values, reached by the API only through the Key Vault references the seed writes |

Every new setting lives in Azure App Configuration and nowhere else: no value goes into `appsettings.json`, `appsettings.Development.json`, `launchSettings.json`, `dotnet user-secrets` or any other committed or git-ignored local settings file. Its default is the property initialiser of its options class, repeated in `infra/appconfig/defaults.json`; a setting without a default (an id, an origin, a secret) exists only under its labels or as a Key Vault reference, and the API refuses to start without it. A host that cannot reach the store gets throwaway values made for that run and never saved as a setting: `ErpApiFactory`'s in-memory defaults in tests, the job's environment in CI, `compose.override.yaml` and a git-ignored file secret in the local containers, the web server's `env` for the store-less API Playwright starts, and the shell's environment for a store-less `dotnet run`. The sign-in settings are the worked example ([configuration](../configuration.md), section "Sign-in settings by process").

`scripts/azure/seed.sh` is the only writer of the seed files' keys. A key whose value identifies a provisioned resource is written by the script that creates the resource and refused in a seed file: `Erp:Platform:Identity:TenantId`, `ClientId` and `WebOrigin` by `entra.sh`, and `Erp:Platform:Attachments:BlobServiceUri` (the development storage account's blob endpoint, label `local-dev`) by `provision.sh` from its deployment output. `Erp:Sentinel` is created under each label by `provision.sh` (which changes an existing value only to bump `local-dev` after it rewrote the blob endpoint), bumped by `seed.sh` on every run unless `--no-sentinel`, and bumped by hand as described below. `node --test "scripts/checks/tests/*.test.ts"` (run by `node scripts/verify/verify.ts` and CI) rejects a seed file that breaks the rules below.

## Naming

- Key: `Erp:<Domain>:<Module>:<Setting>`, exactly four segments in PascalCase (`Erp:Finance:Sales:InvoiceNumberPrefix`). The seed files are nested JSON; `seed.sh` flattens them with `:`.
- Feature flag: `Erp.Modules.<Domain>.<Module>` for a module, `Erp.Modules.<Domain>.<Module>.<Capability>` for a capability (dots, because Microsoft.FeatureManagement forbids colons in a flag name).
- Secret: `Erp--<Domain>--<Module>--<Name>` in Key Vault; the App Configuration key that references it follows the key rule (`Erp:Platform:Identity:ClientCertificate` → `Erp--Platform--Identity--ClientCertificate`).
- Every value is a JSON string (`"8080"`, not `8080`), non-empty and free of control characters, so the repository's flattening and the CLI's agree byte for byte.
- Never in a seed file: a store endpoint, a vault address, an Azure SQL host, a blob storage endpoint (`.blob.core.windows.net`), a tenant, client or subscription id, a connection string or credential (`Server=`, `Password=`, `Secret=`, …, also inside a query string such as a shared access signature's `?sv=…&sig=…`), a key whose name ends in `Secret`, `Password`, `Token`, `ConnectionString`, `ApiKey`, `AccessKey`, `PrivateKey`, `Certificate`, `EncryptionKey` or `EncryptionKeys` as a plain value (those are Key Vault references), an array (`Erp:Platform:Host:KnownNetworks` is host-local and comes from the compose file), a host-local value (`Erp:Platform:Attachments:EmulatorHost` is set only on the machine that runs the API: user secrets for a store-less `dotnet run`, the environment in tests and CI, the compose file for the local container stack), any `Erp:Platform:Configuration:*` value (bootstrap-only, read before the store is connected), the `Erp:Platform:Identity:TenantId`, `ClientId` and `WebOrigin` keys `entra.sh` owns, or the `Erp:Platform:Attachments:BlobServiceUri` key `provision.sh` owns. An endpoint that `provision.sh` or `entra.sh` writes never goes into a seed file, not even under one label: the repository carries no identifier of a provisioned resource.

## Add a setting

1. Bind it: add the property to the module's options class (`AddOptions<T>().BindConfiguration("Erp:<Domain>:<Module>").ValidateDataAnnotations().ValidateOnStart()`), never read `IConfiguration` in feature code.
2. Give it a default as the property initialiser of the options class (`public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(30);` in `EntraSignInOptions`), never in `appsettings*.json`.
3. Seed it: add the same default to `infra/appconfig/defaults.json`. If an environment needs a different value, add the override to `infra/appconfig/local-dev.json` or `infra/appconfig/production.json`; an override without a default is rejected. A value that identifies a provisioned resource is written by its script instead (section above).
4. Give every store-less host its throwaway value when the setting has no default: the in-memory defaults of `ErpApiFactory`, the job environment of `.github/workflows/e2e.yml`, the file secrets `.github/workflows/docker-build.yml` writes for a secret, `infra/compose/compose.override.yaml` and the web server `env` that `frontend/e2e/playwright.config.ts` gives the API it starts without the store.
5. Register it in [docs/configuration.md](../configuration.md) (key, type, default, purpose).
6. Import: `bash scripts/azure/seed.sh --dry-run` to preview, then `bash scripts/azure/seed.sh`. The import writes only keys whose value differs (`--import-mode ignore-match`), never deletes, and bumps the labelled `Erp:Sentinel` so a running API reloads at its next check (30 minutes on the Free tier); restart the api container to apply it at once. A setting the API reads only when it starts reaches a running API through a restart alone, never through a refresh or a sentinel bump: every `Erp:Platform:Identity` value (restart `dotnet run`, or `docker compose … up -d --force-recreate api`; while the token cache is in memory the restart also signs everyone out), the `Erp:Platform:RateLimiting` limits and `Erp:Platform:Attachments:MaxConcurrentUploads` among them.
7. Read it back: `bash scripts/azure/verify.sh --labels`.

A key that must differ per environment gets its unlabelled default in `defaults.json` and the override in the label file; the API loads the unlabelled keys first and the label second, so the label always wins.

## Add a feature flag

1. Declare the module or capability on the module descriptor: the module flag `Erp.Modules.<Domain>.<Module>` exists for every registered module and evaluates enabled until a source switches it off; a `ModuleCapability(Name, EnabledByDefault)` becomes `Erp.Modules.<Domain>.<Module>.<Name>` with the declared default (ADR-0012).
2. Add it to `infra/appconfig/feature-flags.json` with an explicit boolean for both `local-dev` and `production`:

   ```json
   { "id": "Erp.Modules.Finance.Sales", "enabled": { "local-dev": true, "production": false } }
   ```

3. Register it in [docs/configuration.md](../configuration.md) under Feature flags.
4. `bash scripts/azure/seed.sh` creates the flag under each label and enables or disables it to match the file; a later change of the boolean is applied the same way.

## Add a secret

1. Create the secret value in the vault of each environment that needs it, never in the repository. Put each value in a file first (with a text editor or `read -rs`, never as a command argument, so it reaches neither the shell history nor the process list), give each environment its own value, and delete the files afterwards:

   ```bash
   az keyvault secret set --vault-name kv-erp-ai-pro-dev --name Erp--Finance--Gst--ApiKey --file ~/gst-api-key.dev --encoding utf-8 --output none
   az keyvault secret set --vault-name kv-erp-ai-pro-prod --name Erp--Finance--Gst--ApiKey --file ~/gst-api-key.prod --encoding utf-8 --output none
   rm -f ~/gst-api-key.dev ~/gst-api-key.prod
   ```
2. Add the reference to `infra/appconfig/key-vault-references.json`:

   ```json
   { "key": "Erp:Finance:Gst:ApiKey", "secret": "Erp--Finance--Gst--ApiKey" }
   ```

   The same key must not appear as a plain value in any seed file. When only one environment holds the secret, the optional `labels` array restricts the reference to those labels: `seed.sh` writes it and `verify.sh --labels` checks it only under the listed labels, and a label that is not listed gets no reference at all. Omit `labels` when both environments hold the secret.

   ```json
   { "key": "Erp:Platform:Database:ConnectionString", "secret": "Erp--Platform--Database--ConnectionString", "labels": ["local-dev"] }
   ```
3. Register the secret name in [docs/configuration.md](../configuration.md) under Secrets and the key under Configuration keys.
4. `bash scripts/azure/seed.sh` composes the vault URI from `params.env` (`kv-erp-ai-pro-dev` for `local-dev`, `kv-erp-ai-pro-prod` for `production`) for each label the reference applies to, confirms the secret exists in that vault and only then writes the reference; a missing secret stops the run. There are no placeholder secrets: an unresolvable reference fails API startup, and a placeholder would later be read as the real value.
5. Bind the key like any other setting; the API resolves the reference with its own identity (the developer's `az login` locally, the runtime service principal in the container) and caches the value for `Erp:Platform:Configuration:SecretRefreshInterval` (one hour).

## Bump the sentinel by hand

`seed.sh` bumps `Erp:Sentinel` under every label it seeds. To trigger a reload without seeding (for example after `az keyvault secret set` of a new secret version that an existing reference already points to; the sign-in certificate is the exception, because the API reads it only when it starts, [secrets runbook](../operations/runbooks/secrets.md) section 5b), write a new timestamp under the label with the store name from `scripts/azure/params.env` (`ERP_AZURE_APPCONFIG_NAME`, `appcs-erp-ai-pro` today):

```bash
az appconfig kv set --name appcs-erp-ai-pro --auth-mode login --key Erp:Sentinel --label local-dev --value "$(date -u +%Y-%m-%dT%H:%M:%SZ)" --yes
```

The API checks the sentinel every `Erp:Platform:Configuration:RefreshInterval` and reloads every key and flag when it changed; a resolved secret is re-read after `SecretRefreshInterval` regardless of the sentinel.

## Test with a value

Integration tests never reach the store. `ErpApiFactory.WithConfiguration(key, value)` passes the value to the test host as a command-line setting, the highest-priority source in a test host, so it overrides `appsettings.json` and also reaches values the host reads while it is being built (such as `Erp:Platform:Configuration:*`); call it before the first client is created:

```csharp
using var factory = new ErpApiFactory().WithConfiguration("Erp:Platform:Host:ApplicationName", "ERP-AI-Pro (test)");
using var client = factory.CreateClient();
```
