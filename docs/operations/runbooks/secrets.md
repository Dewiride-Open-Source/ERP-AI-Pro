# Secrets runbook

How secrets are named, stored, refreshed, rotated and revoked. Every command runs from the repository root with the parameters in `scripts/azure/params.env` (`ERP_AZURE_APPCONFIG_NAME`, `ERP_AZURE_KEYVAULT_DEV_NAME`, `ERP_AZURE_KEYVAULT_PROD_NAME`); the examples use today's names (`appcs-erp-ai-pro`, `kv-erp-ai-pro-dev`, `kv-erp-ai-pro-prod`).

## 1. Principles

- Key Vault is the only home of a secret value. The API reads a secret only through an App Configuration Key Vault reference (`infra/appconfig/key-vault-references.json`, written by `scripts/azure/seed.sh`), the developer through `az login`, the api container through the runtime service principal's certificate. The one exception is the local container stack, which cannot sign in to Azure and reads its SQL login from the git-ignored file secret `infra/compose/secrets/Erp__Platform__Database__ConnectionString` (section 5d).
- Nothing secret is ever committed: not in code, compose files, workflows, `.env` files, `CLAUDE.md`, memory or documentation. Three layers enforce it: GitHub secret scanning with push protection (repository setting, [github-repository-settings.md](../github-repository-settings.md)), the in-repo check `node scripts/checks/secret-patterns.ts` (every tracked and new file that git does not ignore; runs in `node scripts/verify/verify.ts` and in the `ci-backend` workflow), and `scripts/azure/lib/seed-files.ts`, which refuses credentials and environment identifiers in the seed files.
- A push-protection block or a check finding is fixed at the root: rotate the value in Key Vault (section 5), then remove it from the change. Never bypass the block, never rewrite pushed history to hide a value that has already left the machine.
- Placeholders in committed examples are written as `<name>`, `${VAR}` or `CHANGE_ME`; the check recognises those shapes and reports everything else that looks like a credential.

## 2. Naming

| Kind | Shape | Example |
|---|---|---|
| Key Vault secret, certificate or key | `Erp--<Domain>--<Module>--<Name>` | `Erp--Platform--Identity--ClientCertificate` |
| App Configuration key that references a secret | `Erp:<Domain>:<Module>:<Setting>` | `Erp:Platform:Identity:ClientCertificate` |
| Compose secret file | `<name>.pem` for a certificate, `Erp__<Domain>__<Module>__<Name>` for a configuration value read by the key-per-file provider, under `infra/compose/secrets/` (git-ignored) | `erp-runtime-client.pem`, `Erp__Platform__Database__ConnectionString` |

## 3. Inventory

| Secret | Vault(s) | Created by | Read by | Rotation |
|---|---|---|---|---|
| `Erp--Platform--Identity--ClientCertificate` | `kv-erp-ai-pro-dev`, `kv-erp-ai-pro-prod` | `scripts/azure/entra.sh` (self-signed, 12 months) | the API through the reference `Erp:Platform:Identity:ClientCertificate` (bound by the authentication phase) | section 5b |
| `Erp--Platform--Identity--RuntimeClientCertificate` | `kv-erp-ai-pro-prod` | `scripts/azure/entra.sh` (self-signed, 12 months) | the api container as the compose secret file `erp-runtime-client.pem` (never a reference) | section 5c |
| `Erp--Platform--DataProtection--Key` (key) | both vaults | `scripts/azure/provision.sh` (RSA 2048) | the Data Protection key ring from the authentication phase | that phase's runbook |
| `Erp--Platform--Database--ConnectionString` | `kv-erp-ai-pro-dev` (`kv-erp-ai-pro-prod` from the first-deployment phase) | `az keyvault secret set --file` in the session that introduced the key, with the owner's consent; the local-dev value is the Windows sign-in string `Server=localhost;Database=ErpAiPro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True` and holds no credential | `dotnet run` with the store through the reference `Erp:Platform:Database:ConnectionString` (label `local-dev`; the same value sits in `dotnet user-secrets` for `dotnet ef`); the container stack reads the file secret `Erp__Platform__Database__ConnectionString` instead (a SQL login, never a reference) | section 5d |
| `Erp--Platform--Database--MigratorConnectionString` | `kv-erp-ai-pro-prod` (from the first-deployment database provisioning) | that phase, with the owner's consent; `Authentication="Active Directory Default"`, so it holds no password | the migrator container through the `production` reference `Erp:Platform:Database:MigratorConnectionString` (ADR-0021); locally the migrator reads the compose secret file instead | section 5d, step 3 |

## 4. Refresh intervals

| Interval | Default | Where |
|---|---|---|
| Change check (`Erp:Sentinel` and feature flags) | 30 minutes (Free tier, ADR-0010) | `Erp:Platform:Configuration:RefreshInterval`, bootstrap-only |
| Secret re-read after a Key Vault reference resolved | 1 hour | `Erp:Platform:Configuration:SecretRefreshInterval`, bootstrap-only |
| First load timeout | 1 minute | `Erp:Platform:Configuration:StartupTimeout`, bootstrap-only |

A new secret *version* behind an existing reference is picked up within the secret refresh interval; a new *reference* or any other key change is picked up at the next change check after the labelled sentinel is bumped. `scripts/azure/seed.sh` bumps the sentinel on every run (unless `--no-sentinel`); `provision.sh` never bumps an existing value. By hand, per label:

```bash
az appconfig kv set --name appcs-erp-ai-pro --auth-mode login --key Erp:Sentinel --label production --value "$(date -u +%Y-%m-%dT%H:%M:%SZ)" --yes
```

Recreating the api container applies everything at once: `docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml up -d --force-recreate api`. `--force-recreate` is required: a plain `up -d` leaves a running container untouched when its definition has not changed, so it keeps the old configuration and the old bind-mounted certificate file.

## 5. Rotation

### 5a. Ordinary secret

1. Change the value at its issuer first (the SQL login, the third-party service, the token provider): Key Vault only stores a copy, so a new vault version alone changes nothing and an old value stays valid until the issuer forgets it.
2. Write the new value to a file with an editor or `read -rs` (never as a command argument), then create a new version in the vault of each environment: `az keyvault secret set --vault-name kv-erp-ai-pro-prod --name Erp--<Domain>--<Module>--<Name> --file ~/new-value --encoding utf-8`; delete the file.
3. The reference points at the secret without a version, so the API re-reads it within the secret refresh interval; bump the sentinel (section 4) or recreate the api container to apply it sooner.
4. Once the consumer is confirmed on the new version, disable the previous version: `az keyvault secret set-attributes --vault-name kv-erp-ai-pro-prod --name <secret> --version <old version> --enabled false`.

### 5b. Sign-in certificate (per label)

1. `bash scripts/azure/entra.sh --rotate-signin-certificate production` creates a new certificate version in the label's vault and registers its public key on the sign-in registration alongside the current one.
2. Bump the sentinel for that label or recreate the api container; sign in through the browser and confirm the session works.
3. `bash scripts/azure/entra.sh --prune-old-credentials production` removes every key credential that is not the current certificate; `bash scripts/azure/verify.sh --entra` must stay green.

### 5c. Runtime certificate (the container's own identity)

1. `bash scripts/azure/entra.sh --rotate-runtime-certificate` creates a new version in `kv-erp-ai-pro-prod` and registers it on the runtime registration.
2. `bash scripts/azure/export-runtime-certificate.sh` downloads the PEM (private key included) to `scripts/azure/out/erp-runtime-client.pem` (git-ignored). The script refuses to overwrite, so a copy left from the previous rotation must be shredded first (step 6).
3. Copy it to the server over SSH only: `scp scripts/azure/out/erp-runtime-client.pem <user>@<server>:~/erp-runtime-client.pem`; then, on the server from the deployment checkout: `install -d -m 0700 infra/compose/secrets && install -o 1654 -g 1654 -m 0400 ~/erp-runtime-client.pem infra/compose/secrets/erp-runtime-client.pem` (1654 is the api container user).
4. `docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml up -d --force-recreate api` (the secret is a bind-mounted file, and a plain `up -d` keeps the running container with the old one); `/healthz/ready` must report Healthy.
5. `bash scripts/azure/entra.sh --prune-old-credentials runtime`, then `bash scripts/azure/verify.sh --entra`.
6. Shred both staging copies, on the server `shred -u ~/erp-runtime-client.pem` and on the workstation `shred -u scripts/azure/out/erp-runtime-client.pem` (`rm -P` on macOS; on Windows delete it from a volume without shadow copies). Only `infra/compose/secrets/erp-runtime-client.pem` on the server remains.

### 5d. Database connection string

The value differs per consumer, so rotate the one that changed:

1. Host processes on the owner's machine (`dotnet run`, `dotnet ef`, tests) sign in with Windows authentication, so `Erp--Platform--Database--ConnectionString` in `kv-erp-ai-pro-dev` carries no credential and changes only when the server or database name does: set a new secret version (5a step 2), bump the `local-dev` sentinel (section 4) or restart `dotnet run`, and set the same value again with `dotnet user-secrets set "Erp:Platform:Database:ConnectionString" ... --project Hosts/Api/Dewiride.Erp.Host.Api` from `backend/` so `dotnet ef` follows.
2. The container stack signs in with a SQL login carried by the file secret `infra/compose/secrets/Erp__Platform__Database__ConnectionString`: change the login's password in SQL Server first (`ALTER LOGIN <login> WITH PASSWORD = ...` from SQL Server Management Studio, the same rule as 5a step 1), rewrite the file with the new password, then `docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml up -d --force-recreate api`.
   The migrator container signs in as the separate login `erp_local_migrator` (`db_ddladmin`, `db_datareader`, `db_datawriter` on `ErpAiPro`) carried by `infra/compose/secrets/Erp__Platform__Database__MigratorConnectionString`; rotate it the same way (new password in SQL Server first, then the file) and rerun the stack, which runs the migrator before the api.
3. Production (from the first-deployment phase) uses `Authentication="Active Directory Default"` and holds no password: a new version in `kv-erp-ai-pro-prod` (5a step 2), a `production` sentinel bump and `docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml up -d --force-recreate api`.

### 5e. Data-protection key

Rotation of `Erp--Platform--DataProtection--Key` is written by the authentication phase, which introduces its consumer; until then the key exists only so the role assignments are meaningful.

## 6. Emergency revocation

Order: revoke, rotate, investigate — never investigate first.

1. Revoke the exposed credential immediately. Certificate: `az ad app credential delete --id <appId> --key-id <keyId> --cert` (the key id is listed by `az ad app credential list --id <appId> --cert`; without `--cert` the command lists password credentials, of which there must be none). Ordinary secret: revoke it at the issuer (drop or reset the SQL login, revoke the token at the provider), then disable the vault version: `az keyvault secret set-attributes --vault-name <vault> --name <secret> --version <version> --enabled false`.
2. Rotate as in section 5, bump the sentinel for every affected label and recreate the api container with `--force-recreate`.
3. Investigate with the API and container logs, the vault's diagnostic log and the Entra sign-in log; record what was exposed, when, and what was rotated.
4. If the value reached git history, rotate first and only then decide with the owner whether history is rewritten; a rotated value is worthless, an unrotated one stays dangerous whatever happens to the history.

## 7. Quarterly access review

- `bash scripts/azure/verify.sh --entra` warns about a certificate that expires within 30 days; rotate it (section 5b or 5c) before it does.
- `bash scripts/azure/verify.sh` confirms the role matrix (operator, developers group, runtime service principal); remove people who left the developers group in Entra.
- Confirm in GitHub → Settings → Code security that secret scanning and push protection are still enabled and that no open alert exists.
- Confirm `infra/compose/secrets/` on the server holds only `erp-runtime-client.pem` with owner 1654 and mode 0400.
