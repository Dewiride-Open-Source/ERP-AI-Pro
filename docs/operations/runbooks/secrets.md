# Secrets runbook

How secrets are named, stored, refreshed, rotated and revoked. Every command runs from the repository root with the parameters in `scripts/azure/params.env` (`ERP_AZURE_APPCONFIG_NAME`, `ERP_AZURE_KEYVAULT_DEV_NAME`, `ERP_AZURE_KEYVAULT_PROD_NAME`); the examples use today's names (`appcs-erp-ai-pro`, `kv-erp-ai-pro-dev`, `kv-erp-ai-pro-prod`).

## 1. Principles

- Key Vault is the only home of a secret value. The API reads a secret only through an App Configuration Key Vault reference (`infra/appconfig/key-vault-references.json`, written by `scripts/azure/seed.sh`), the developer through `az login`, the api container through the runtime service principal's certificate. A developer machine additionally keeps copies of development values in `dotnet user-secrets` for `dotnet ef` and store-less runs (the database connection string, the development attachments keys), written without display and never committed. The only secrets with no home in a vault belong to the local container stack, which cannot sign in to Azure and reads its SQL logins and its attachments encryption key from git-ignored file secrets under `infra/compose/secrets/` (sections 5d and 5f).
- Storage accounts hold no secret at all. Every account has Shared Key authorization disabled (`allowSharedKeyAccess` `false`), so no account key, connection string or key-signed shared access signature can reach its data; the API, the developers and the scripts reach Blob Storage only with an Entra ID token and a data role. Never list, rotate or copy storage account keys: they grant nothing and are not part of any procedure here.
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
| the migrator service principal's certificate | `kv-erp-ai-pro-prod` (from the first-deployment database provisioning) | that phase, with the owner's consent (self-signed, 12 months) | the migrator container as the compose secret file `erp-migrator-client.pem` (never a reference) | section 5c, applied to the migrator service principal and its file |
| `Erp--Platform--Attachments--EncryptionKey` | `kv-erp-ai-pro-dev` (`kv-erp-ai-pro-prod` from the first-deployment phase, with its own value) | the operator, once per vault and at every rotation: 32 random bytes generated inside the block of section 5f (for the development vault's first key, equally the local-development block, which generates one only when the vault holds none) and written with `az keyvault secret set --file`, never displayed; value `<key id>:<32 bytes, base64>` | the API through the reference `Erp:Platform:Attachments:EncryptionKey` (label `local-dev`), as the key that wraps every attachment's data key; a store-less `dotnet run` (and the gated API local Playwright starts) reads the same value from `dotnet user-secrets`, copied there without display by the blocks of section 5f and [docs/guides/local-development.md](../../guides/local-development.md); the local container stack reads the file secret `infra/compose/secrets/Erp__Platform__Attachments__EncryptionKey` instead (a separate key of the same shape; the migrator never reads either) | section 5f |
| `Erp--Platform--Attachments--RetiredEncryptionKeys` | the vault whose key was rotated; absent until the first rotation | section 5f, step 1 (the reference: step 2) | the API through the reference `Erp:Platform:Attachments:RetiredEncryptionKeys`, only to read attachments wrapped by an earlier key; a store-less `dotnet run` reads the same list from `dotnet user-secrets` | section 5f |

## 4. Refresh intervals

| Interval | Default | Where |
|---|---|---|
| Change check (`Erp:Sentinel` and feature flags) | 30 minutes (Free tier, ADR-0010) | `Erp:Platform:Configuration:RefreshInterval`, bootstrap-only |
| Secret re-read after a Key Vault reference resolved | 1 hour | `Erp:Platform:Configuration:SecretRefreshInterval`, bootstrap-only |
| First load timeout | 1 minute | `Erp:Platform:Configuration:StartupTimeout`, bootstrap-only |

A new secret *version* behind an existing reference is picked up within the secret refresh interval; a new *reference* or any other key change is picked up at the next change check after the labelled sentinel is bumped. `scripts/azure/seed.sh` bumps the sentinel on every run (unless `--no-sentinel`); `provision.sh` bumps the `local-dev` sentinel only when it changes `Erp:Platform:Attachments:BlobServiceUri` and otherwise never bumps an existing value. By hand, per label:

```bash
az appconfig kv set --name appcs-erp-ai-pro --auth-mode login --key Erp:Sentinel --label production --value "$(date -u +%Y-%m-%dT%H:%M:%SZ)" --yes
```

Recreating the api container applies everything at once: `docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml up -d --force-recreate api`. `--force-recreate` is required: a plain `up -d` leaves a running container untouched when its definition has not changed, so it keeps the old configuration and the old bind-mounted certificate file.

## 5. Rotation

### 5a. Ordinary secret

1. Change the value at its issuer first (the SQL login, the third-party service, the token provider): Key Vault only stores a copy, so a new vault version alone changes nothing and an old value stays valid until the issuer forgets it.
2. Write the new value to a file with an editor or `read -rs` (never as a command argument), then create a new version in the vault of each environment: `az keyvault secret set --vault-name kv-erp-ai-pro-prod --name Erp--<Domain>--<Module>--<Name> --file ~/new-value --encoding utf-8 --output none` (without `--output none` the command prints the secret it stored); delete the file.
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

### 5f. Attachments encryption key

`Erp--Platform--Attachments--EncryptionKey` is the key-encryption key of the attachments envelope: the API wraps every attachment's random data key with it (AES-256-GCM) before the ciphertext reaches Blob Storage. Its value is `<key id>:<32 random bytes, base64>`; the key id is 1 to 32 characters from `A-Z`, `a-z`, `0-9`, `.`, `_` and `-`, is stored in plain text in every envelope header and in `files.StoredContents.KeyId`, and must never be reused: the API refuses a configuration in which one id names two different keys. The key itself is never typed, pasted, echoed or passed as a command argument, and it has no issuer: the vault is its home (a developer machine keeps a copy of the development keys in `dotnet user-secrets` for store-less runs; production keys exist nowhere else), so losing every copy of a key makes every attachment it wrapped unreadable (soft delete and purge protection on the vault guard against that).

The block below creates the key once per vault (the `local-dev` reference in `infra/appconfig/key-vault-references.json` makes `seed.sh` stop until the development secret exists) and every new current key of a rotation (step 4). It runs from the repository root in a subshell that stops at the first failed command, so a failed read or key generation never uploads a malformed key. It refuses to replace a current key that `Erp--Platform--Attachments--RetiredEncryptionKeys` does not list, so it can never strand the attachments that key wrapped. The bytes are generated inside it, every value passes through private temporary files that are shredded when the block ends, whether or not a step succeeded, and `--output none` keeps the stored value off the screen. With `user_secrets=true` (the development vault, on the operator's machine) it also pipes the same value as JSON into `dotnet user-secrets set` for the API project, which merges it into the secrets already there with the tool's output discarded, so store-less runs use the same key; every other developer copies it with the block in [docs/guides/local-development.md](../../guides/local-development.md), section "Attachments storage", which also creates the development key when the vault has none.

```bash
(
  set -euo pipefail
  umask 077
  vault=kv-erp-ai-pro-dev
  prefix=dev
  user_secrets=true
  key_file="$(mktemp)"
  current_file="$(mktemp)"
  retired_file="$(mktemp)"
  trap 'shred -u "$key_file" "$current_file" "$retired_file"' EXIT
  key_count="$(az keyvault secret list --vault-name "$vault" --query "length([?name=='Erp--Platform--Attachments--EncryptionKey'])" --output tsv | tr -d '\r')"
  retired_count="$(az keyvault secret list --vault-name "$vault" --query "length([?name=='Erp--Platform--Attachments--RetiredEncryptionKeys'])" --output tsv | tr -d '\r')"
  if [ "$key_count" != 0 ]; then
    az keyvault secret show --vault-name "$vault" --name Erp--Platform--Attachments--EncryptionKey --query value --output tsv | tr -d '\r\n' > "$current_file"
    if [ "$retired_count" = 1 ]; then
      az keyvault secret show --vault-name "$vault" --name Erp--Platform--Attachments--RetiredEncryptionKeys --query value --output tsv | tr -d '\r\n' | tr ';' '\n' > "$retired_file"
    fi
    if ! grep -qxFf "$current_file" "$retired_file"; then
      echo "The current attachments key in $vault is not in Erp--Platform--Attachments--RetiredEncryptionKeys; retire it first (rotation step 1)." >&2
      exit 1
    fi
  fi
  material="$(openssl rand -base64 32)"
  printf '%s-%s:%s' "$prefix" "$(date -u +%Y%m%d%H%M%S)" "$material" > "$key_file"
  az keyvault secret set --vault-name "$vault" --name Erp--Platform--Attachments--EncryptionKey --file "$key_file" --encoding utf-8 --output none
  if [ "$user_secrets" = true ]; then
    { printf '{"Erp:Platform:Attachments:EncryptionKey":"'; cat "$key_file"; printf '"}'; } | dotnet user-secrets set --project backend/Hosts/Api/Dewiride.Erp.Host.Api > /dev/null
  fi
)
```

The production key comes with the `first-deployment` phase from the same block with `vault=kv-erp-ai-pro-prod`, `prefix=prod` and `user_secrets=false`, never by copying the development value. The local container stack uses a separate key of the same shape in the file secret `infra/compose/secrets/Erp__Platform__Attachments__EncryptionKey`, created as [docs/guides/local-development.md](../../guides/local-development.md) describes; it protects only the attachments the stack keeps in its storage emulator and never enters a vault.

Rotation, per vault (the example rotates `kv-erp-ai-pro-dev` under `local-dev`). Attachments already stored stay wrapped by the key that wrote them, so the old key moves to `Erp--Platform--Attachments--RetiredEncryptionKeys`, a `;`-separated list of `<key id>:<key>` entries the API reads only to decrypt. The API accepts the same key in both settings (an id that names two different keys is still refused), so the current key is retired first and replaced afterwards, and every state the vault passes through is a valid configuration: a restart, a sentinel bump or the hourly secret re-read of section 4 at any moment of the rotation is harmless, and the steps need not run in one sitting. The one ordering rule is step 3: every running API must hold the retired list of step 1 before the current key changes, because an API that picks up the new current key while still holding an older retired list keeps uploading but answers 404 `attachment.not-found` for every attachment the old key wrapped until it next reloads.

1. Append the current value to the retired list without displaying either. The block stops at the first failed command, so a failed read never writes a shortened list; running it twice leaves a duplicate entry, which the API ignores. With `user_secrets=true` (the development vault, on the operator's machine) it also copies the new list into your user secrets, so your store-less runs keep reading the old attachments after step 4:

   ```bash
   (
     set -euo pipefail
     umask 077
     vault=kv-erp-ai-pro-dev
     user_secrets=true
     retired_file="$(mktemp)"
     trap 'shred -u "$retired_file"' EXIT
     retired_count="$(az keyvault secret list --vault-name "$vault" --query "length([?name=='Erp--Platform--Attachments--RetiredEncryptionKeys'])" --output tsv | tr -d '\r')"
     if [ "$retired_count" = 1 ]; then
       az keyvault secret show --vault-name "$vault" --name Erp--Platform--Attachments--RetiredEncryptionKeys --query value --output tsv | tr -d '\r\n' > "$retired_file"
       printf ';' >> "$retired_file"
     fi
     az keyvault secret show --vault-name "$vault" --name Erp--Platform--Attachments--EncryptionKey --query value --output tsv | tr -d '\r\n' >> "$retired_file"
     az keyvault secret set --vault-name "$vault" --name Erp--Platform--Attachments--RetiredEncryptionKeys --file "$retired_file" --encoding utf-8 --output none
     if [ "$user_secrets" = true ]; then
       { printf '{"Erp:Platform:Attachments:RetiredEncryptionKeys":"'; cat "$retired_file"; printf '"}'; } | dotnet user-secrets set --project backend/Hosts/Api/Dewiride.Erp.Host.Api > /dev/null
     fi
   )
   ```

2. On the first rotation only, add `{ "key": "Erp:Platform:Attachments:RetiredEncryptionKeys", "secret": "Erp--Platform--Attachments--RetiredEncryptionKeys", "labels": ["local-dev"] }` to `infra/appconfig/key-vault-references.json` in your working copy (listing each label whose vault holds the secret) and write the reference with `bash scripts/azure/seed.sh --label local-dev`, which also bumps the label's sentinel. Commit the change in a pull request afterwards like any other seed change.
3. Make sure every running API holds the new retired list: restart `dotnet run` (every developer who runs it against the store) and recreate the api container (`--force-recreate`, section 4), or wait until both the change check after the sentinel bump and the secret refresh interval have passed (section 4: 30 minutes and 1 hour). A store-less `dotnet run` reads the list from user secrets: step 1 put it in yours, and every other developer runs the block in [docs/guides/local-development.md](../../guides/local-development.md), section "Attachments storage", before restarting.
4. Create the new current key with the creation block above, with the same `vault`, `prefix` and `user_secrets`: it finds the current key in the retired list, writes a new version of `Erp--Platform--Attachments--EncryptionKey` with a new id and, with `user_secrets=true`, puts the same value in your user secrets.
5. Bump the sentinel of the label (section 4), or restart `dotnet run` and recreate the api container, so every API encrypts with the new key; every other developer runs the local-development block again, which copies the new current key and the retired list into their user secrets.
6. Confirm that an attachment uploaded before the rotation still downloads and that a new upload succeeds, then disable the previous version of `Erp--Platform--Attachments--EncryptionKey` (5a step 4); the retired list now holds the old key's only live copy in the vault. The ids in `files.StoredContents.KeyId` show which attachments each retired key still protects; a retired entry is removed only after no row in any database that uses the vault names its id (for the development vault that is every developer's `ErpAiPro`, so development keys stay retired).

Never write a new current key with a bare `az keyvault secret set`: a key that replaces one the retired list does not hold makes every attachment the old key wrapped unreadable until the old key is taken from the secret's previous version and retired as in step 1.

After an exposure, rotate the same way and treat every attachment wrapped by the exposed key as readable by whoever holds it together with blob read access: Blob Storage never received the key, so the exposure matters only in combination with a storage role or the account itself. A copy of the development key sits in the user secrets of every developer machine that ran the local-development block, so a lost or compromised developer machine counts as an exposure of the development key.

## 6. Emergency revocation

Order: revoke, rotate, investigate — never investigate first.

1. Revoke the exposed credential immediately. Certificate: `az ad app credential delete --id <appId> --key-id <keyId> --cert` (the key id is listed by `az ad app credential list --id <appId> --cert`; without `--cert` the command lists password credentials, of which there must be none). Ordinary secret: revoke it at the issuer (drop or reset the SQL login, revoke the token at the provider), then disable the vault version: `az keyvault secret set-attributes --vault-name <vault> --name <secret> --version <version> --enabled false`.
2. Rotate as in section 5, bump the sentinel for every affected label and recreate the api container with `--force-recreate`.
3. Investigate with the API and container logs, the vault's diagnostic log and the Entra sign-in log; record what was exposed, when, and what was rotated.
4. If the value reached git history, rotate first and only then decide with the owner whether history is rewritten; a rotated value is worthless, an unrotated one stays dangerous whatever happens to the history.

## 7. Quarterly access review

- `bash scripts/azure/verify.sh --entra` warns about a certificate that expires within 30 days; rotate it (section 5b or 5c) before it does.
- `bash scripts/azure/verify.sh` confirms the role matrix (operator, developers group, runtime service principal), including Storage Blob Data Contributor on the development `attachments` container; remove people who left the developers group in Entra. List every role that reaches the development `attachments` container, including those inherited from the account, the resource group and the subscription, with `az role assignment list --scope <account resource id>/blobServices/default/containers/attachments --include-inherited --output table`: only the operator and the developers group hold a Storage Blob Data role there, and a Storage Account Contributor, Storage Blob Data Owner or Storage Blob Delegator assignment at any of those scopes is removed (for an assignment on the account or its container, the account lock must be lifted first and `provision.sh` run afterwards, [azure-bootstrap.md](../azure-bootstrap.md) section 8).
- `az storage account show --name <account> --resource-group rg-erp-ai-pro --query allowSharedKeyAccess` still prints `false`; `verify.sh` checks the same, and anything else means someone re-enabled the account keys.
- Confirm in GitHub → Settings → Code security that secret scanning and push protection are still enabled and that no open alert exists.
- Confirm `infra/compose/secrets/` on the server holds only `erp-runtime-client.pem` and, once the first-deployment database provisioning created it, `erp-migrator-client.pem`, each with owner 1654 (the api image user, which the migrator shares) and mode 0400.
