# Local development

## Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.0.x (band pinned in `backend/global.json`) | `dotnet --version` |
| Node.js | 24 LTS | `node --version` |
| pnpm | 12 | `npm i -g pnpm@12` then `pnpm --version` |
| Docker Desktop | current, with Compose v2; also runs the Azurite storage emulator the backend tests need (section "Attachments storage") | `docker compose version` |
| Azure CLI | current, with Bicep (`az bicep install`), signed in to the Dewiride tenant | `az account show`, `az bicep version` |
| SQL Server | SQL Server 2025 Developer (default instance, compatibility level 170); Windows sign-in for host processes; TCP/IP, mixed-mode sign-in and a SQL login only for the container stack (section "Containers") | SQL Server Management Studio |
| Git | `core.longpaths true` on Windows | `git config core.longpaths` |

## First run

```bash
git clone https://github.com/Dewiride-Open-Source/ERP-AI-Pro.git
cd ERP-AI-Pro

# backend
cd backend
dotnet restore
dotnet tool restore                                        # dotnet-ef and kiota from .config/dotnet-tools.json
dotnet build --no-restore -warnaserror
dotnet user-secrets set "Erp:Platform:Database:ConnectionString" "Server=localhost;Database=ErpAiPro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" --project Hosts/Api/Dewiride.Erp.Host.Api
dotnet user-secrets set "Erp:Platform:Attachments:BlobServiceUri" "https://sterpaiprodev.blob.core.windows.net/" --project Hosts/Api/Dewiride.Erp.Host.Api
# before the first dotnet run, run the block in section "Attachments storage" (from the repository root): it writes the attachments encryption key to user secrets without displaying it, and the API refuses to start without the key
node ../scripts/ef/ef.ts update --all                     # every context; creates ErpAiPro when it does not exist (docs/guides/migrations.md)
dotnet test --solution Dewiride.Erp.slnx                   # needs ERP_TEST_SQL_CONNECTION, ERP_TEST_BLOB_EMULATOR_HOST and Azurite running (sections "Configuration and secrets", "Attachments storage")
dotnet run --project Hosts/Api/Dewiride.Erp.Host.Api      # http://localhost:5080; attachments go to the development storage account as your az login identity (section "Attachments storage")

# frontend (second terminal)
cd frontend
pnpm install --frozen-lockfile
pnpm dev                                                   # builds @dewiride/erp-api-client, then http://localhost:3000 → redirects to /login
```

`http://localhost:3000/platform/system-info` shows data the web server reads through the generated API client (`shared/api/client.ts`); the browser reaches `/api/*` through the rewrite in `proxy.ts`. `http://localhost:5080/scalar` shows the API reference in Development.

## Configuration and secrets

- Non-secret defaults are in `backend/Hosts/Api/Dewiride.Erp.Host.Api/appsettings.json` and `appsettings.Development.json`. The Development file raises `Microsoft.AspNetCore` logging to `Information` but keeps `Microsoft.AspNetCore.Hosting.Diagnostics` at `Warning`: its request-start and request-finish lines print the query string, which carries attachment download tokens ([observability](observability.md), section "Attachments").
- On a developer machine every request, from the browser through the web server's rewrite and from the web server's own calls, reaches the API from the loopback address and so shares one anonymous rate-limit partition. The `local-dev` label therefore sets `Erp:Platform:RateLimiting:AnonymousPermitLimit` to 6000 per minute (`infra/appconfig/local-dev.json`), enough for a Playwright run over all five browser projects; a store-less run keeps the default 600, and `dotnet user-secrets set "Erp:Platform:RateLimiting:AnonymousPermitLimit" "6000" --project Hosts/Api/Dewiride.Erp.Host.Api` gives it the same allowance when a local run answers 429 `rate-limit.exceeded`.
- The API picks its configuration source at startup. With `APPCONFIG_ENDPOINT` set it loads Azure App Configuration with the `local-dev` label and resolves the Key Vault references from the local-dev vault (including the SQL Server connection string and the attachments encryption key); without it a Development host reads its configuration from `appsettings.json` plus `dotnet user-secrets` only, and the one Azure service it still contacts is the development storage account that holds attachments (section "Attachments storage"). `dotnet ef` always runs the host without the store: the host sets `ERP_CONFIGURATION_SOURCE=LocalDevelopment` at EF design time, so migrations never depend on Azure.
- The database connection string has three homes on a developer machine, all Windows sign-in, none of them a committed file:
  - `dotnet run` with the store reads the Key Vault reference `Erp:Platform:Database:ConnectionString` (secret `Erp--Platform--Database--ConnectionString` in the local-dev vault, created with `az keyvault secret set --file` in the session that introduced the key, with the owner's consent).
  - `dotnet ef` and a store-less `dotnet run` read `dotnet user-secrets`: `cd backend && dotnet user-secrets set "Erp:Platform:Database:ConnectionString" "Server=localhost;Database=ErpAiPro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" --project Hosts/Api/Dewiride.Erp.Host.Api`.
  - Tests read the user environment variable `ERP_TEST_SQL_CONNECTION`, a server-level string without `Database=`: `setx ERP_TEST_SQL_CONNECTION "Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"`; `setx` writes the user environment, which only shells opened afterwards see.
- Tests also read the user environment variable `ERP_TEST_BLOB_EMULATOR_HOST`, the host of a running Azurite (section "Attachments storage").
- To use the store: `az login --tenant <tenant id>` once, then `cd backend && dotnet user-secrets set APPCONFIG_ENDPOINT https://<store>.azconfig.io --project Hosts/Api/Dewiride.Erp.Host.Api`. `ERP_ENVIRONMENT=local-dev` and `AZURE_TOKEN_CREDENTIALS=AzureCliCredential` (which makes `DefaultAzureCredential` use the `az login` session and nothing else) come from `Properties/launchSettings.json`; `APPCONFIG_ENDPOINT` itself is never committed. The store's values take precedence over user secrets, the attachments endpoint and encryption key among them (section "Attachments storage").
- Any other machine-specific value goes to `dotnet user-secrets` (backend) and `frontend/apps/web/.env.local` (never committed). The web app validates its variables at startup (`src/shared/config/env.schema.ts`, documented in `frontend/apps/web/.env.example`): `API_INTERNAL_URL` defaults to `http://localhost:5080` in development and is mandatory when `NODE_ENV=production`; `NEXT_PUBLIC_APP_NAME` is inlined by `next build`, so changing it means rebuilding.
- Access to the store and the local-dev vault and the group to join are in [docs/operations/azure-bootstrap.md](../operations/azure-bootstrap.md), section "Developer onboarding".
- The refresh intervals under `Erp:Platform:Configuration` are read from `appsettings.json` before the store is connected; a store value never changes them.
- Integration tests never reach the store: every `ErpApiFactory` blanks `APPCONFIG_ENDPOINT` and forces the in-memory source, so the endpoint in your user secrets cannot switch a test host to Azure (a Development test host still loads the user-secrets file itself, exactly as `dotnet run` does). The `SqlTestDatabase` assembly fixture of every SQL-backed test project creates `ErpAiProTest_<yyyyMMddHHmmss>_<8 hex>` from `ERP_TEST_SQL_CONNECTION` (rights to create databases required), migrates it and drops it when the test process ends; `ErpApiFactory` points every test host at that database, so the user-secrets connection string never reaches a test. Likewise the `BlobTestContainer` assembly fixture creates the storage container `erptest-<yyyyMMddHHmmss>-<8 hex>` on the Azurite at `ERP_TEST_BLOB_EMULATOR_HOST` and deletes it when the test process ends, and `ErpApiFactory` points every test host at that container, so no test reaches the development storage account.

## Attachments storage

Attachments live in Azure Blob Storage, encrypted by the API with its own key before they leave it, so storage only ever holds unreadable data. No storage account key is used or stored anywhere: the account has Shared Key authorization disabled and the API signs in with Entra ID, or it talks to Azurite, Microsoft's Blob Storage emulator, through the emulator's well-known development account.

- `dotnet run` and the APIs that local Playwright starts use the development storage account (`sterpaiprodev` unless `ERP_AZURE_STORAGE_DEV_NAME` in `scripts/azure/params.env` names another) and sign in to it with your `az login` session (`AzureCliCredential`). Your account needs the **Storage Blob Data Contributor** role on the account's `attachments` container ([docs/operations/azure-bootstrap.md](../operations/azure-bootstrap.md), section "Developer onboarding"); a new role assignment can take up to 10 minutes to take effect, and storage refuses uploads and downloads until it has.
- With the store, `scripts/azure/provision.sh` writes the account's endpoint as `Erp:Platform:Attachments:BlobServiceUri` under the `local-dev` label, and the encryption key arrives through the Key Vault reference `Erp:Platform:Attachments:EncryptionKey` (secret `Erp--Platform--Attachments--EncryptionKey` in the local-dev vault, value `<key id>:<32 random bytes in base64>`).
- Without the store (the "First run" block, and the gated API that Playwright starts with `APPCONFIG_ENDPOINT` blanked) both come from user secrets: `Erp:Platform:Attachments:BlobServiceUri` holds the same endpoint (`provision.sh` prints it as `ATTACHMENTS_BLOB_ENDPOINT_DEV`), and `Erp:Platform:Attachments:EncryptionKey` holds a copy of the vault's key (with `Erp:Platform:Attachments:RetiredEncryptionKeys` once the key has been rotated), written by the block below. Runs with and without the store therefore share one key and can each download what the other uploaded; whenever the store is connected its values take precedence.
- The block runs from the repository root in Git Bash, signed in with `az login` to the Dewiride tenant. When the local-dev vault already holds `Erp--Platform--Attachments--EncryptionKey`, as it does for everyone after the first time, the block copies that key, and the retired keys when a rotation left any, into your user secrets. When the vault holds no key, which happens once per vault, it generates one and writes it to the vault before your user secrets; only the operator (Key Vault Secrets Officer) can write there, and for anyone else the block stops at the vault with nothing written. It never replaces a key the vault already holds (rotation is [docs/operations/runbooks/secrets.md](../operations/runbooks/secrets.md), section 5f). No key is ever displayed or passed as a command argument: the value goes from Key Vault or `openssl` into a private temporary file, from there to the vault with `--file` and to `dotnet user-secrets set` as JSON on standard input (which merges into the secrets already there, with the tool's own output discarded), and the file is shredded when the block ends, whether or not a step failed. The subshell stops at the first failed command, so a failed read or generation never stores a partial key. Run it again after every rotation of the development key.

```bash
(
  set -euo pipefail
  umask 077
  vault=kv-erp-ai-pro-dev
  key_file="$(mktemp)"
  retired_file="$(mktemp)"
  trap 'shred -u "$key_file" "$retired_file"' EXIT
  key_count="$(az keyvault secret list --vault-name "$vault" --query "length([?name=='Erp--Platform--Attachments--EncryptionKey'])" --output tsv | tr -d '\r')"
  retired_count="$(az keyvault secret list --vault-name "$vault" --query "length([?name=='Erp--Platform--Attachments--RetiredEncryptionKeys'])" --output tsv | tr -d '\r')"
  if [ "$key_count" = 1 ]; then
    az keyvault secret show --vault-name "$vault" --name Erp--Platform--Attachments--EncryptionKey --query value --output tsv | tr -d '\r\n' > "$key_file"
    if [ "$retired_count" = 1 ]; then
      az keyvault secret show --vault-name "$vault" --name Erp--Platform--Attachments--RetiredEncryptionKeys --query value --output tsv | tr -d '\r\n' > "$retired_file"
    fi
  elif [ "$key_count" = 0 ]; then
    material="$(openssl rand -base64 32)"
    printf 'dev-%s:%s' "$(date -u +%Y%m%d%H%M%S)" "$material" > "$key_file"
    az keyvault secret set --vault-name "$vault" --name Erp--Platform--Attachments--EncryptionKey --file "$key_file" --encoding utf-8 --output none
  else
    echo "Unexpected answer from Key Vault: '$key_count'" >&2
    exit 1
  fi
  {
    printf '{"Erp:Platform:Attachments:EncryptionKey":"'
    cat "$key_file"
    if [ -s "$retired_file" ]; then
      printf '","Erp:Platform:Attachments:RetiredEncryptionKeys":"'
      cat "$retired_file"
    fi
    printf '"}'
  } | dotnet user-secrets set --project backend/Hosts/Api/Dewiride.Erp.Host.Api > /dev/null
)
```

`dotnet user-secrets list --project backend/Hosts/Api/Dewiride.Erp.Host.Api | cut -d= -f1` then lists the setting names without their values.

- `Erp:Platform:Attachments:EmulatorHost` never belongs in your user secrets: the API refuses to start when both a storage account and an emulator are configured.
- Every developer shares the development `attachments` container, and a blob is named by its content id alone, with nothing that says whose database refers to it: never delete blobs there in bulk. Dropping your local database leaves the blobs of its attachments behind (encrypted, and swept by nothing, because the sweeper works only from the upload reservations in your own database); they cost only storage.
- Backend tests always use Azurite, never the development account. Start it once in Docker Desktop and set the variable the test fixture reads; `--restart unless-stopped` brings it back after a reboot, `--inMemoryPersistence` keeps nothing on disk, and the port is published on loopback only:

```bash
docker run --detach --name erp-azurite --restart unless-stopped --publish 127.0.0.1:10000:10000 \
  mcr.microsoft.com/azure-storage/azurite:3.37.0@sha256:830430c1da1a2d537e08f3e6764dd1f5ae00cf0346bcaf625b968ec3f0971fd5 \
  azurite-blob --blobHost 0.0.0.0 --blobPort 10000 --inMemoryPersistence --disableTelemetry
setx ERP_TEST_BLOB_EMULATOR_HOST 127.0.0.1
```

The image reference is the one in `infra/compose/compose.override.yaml`, which Dependabot keeps current; `scripts/checks/tests/azurite-pin.test.ts` fails while this guide or `.github/actions/start-azurite` names another. After a bump, `docker rm --force erp-azurite` and run the command again. Only the tests use this container; the compose stack runs its own (section "Containers").

## Containers

`docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml --profile observability up -d --wait` starts the API and web images plus the Aspire dashboard (`http://localhost:18888`) for local telemetry; the containers send it telemetry once `OTEL_EXPORTER_OTLP_ENDPOINT` and `WEB_OTEL_EXPORTER_OTLP_ENDPOINT` are set in `infra/compose/.env` ([observability](observability.md)). No SQL Server container exists: the api container reaches the owner's SQL Server through `host.docker.internal` (Docker Desktop maps it to the host) with a SQL login, because a container cannot use Windows sign-in. Attachments go to the stack's own `azurite` service (`compose.override.yaml`: data in the named volume `azurite-data`, no port published to the host); the api container reaches it as `azurite` through `Erp__Platform__Attachments__EmulatorHost`, starts only once it is healthy, and never signs in to Azure storage. The stack has no edge proxy yet, so the web container (`http://localhost:3000`) rewrites `/api/*` to the api container itself, and it runs the image's standalone server, which keeps Next.js's 10 MB request body buffer (`experimental.proxyClientMaxBodySize` is raised to `"101mb"` only for `next dev` and `next start`): an upload over 10 MB through `http://localhost:3000` is cut off at 10 MB (the web container logs `Request body exceeded 10MB`) and receives no answer, so the page keeps showing it as uploading. Send larger files to the API port (`http://localhost:5080/api/platform/attachments`), or use `pnpm dev` or `next start`, whose rewrite accepts up to the 100 MiB ceiling. Before the first `up`:

1. Enable TCP/IP for the instance on port 1433 (SQL Server Configuration Manager → SQL Server Network Configuration → Protocols → TCP/IP → Enabled, IPAll → TCP Port 1433) and restart the SQL Server service.
2. Enable mixed-mode sign-in (SQL Server Management Studio → server Properties → Security → SQL Server and Windows Authentication mode) and restart the service.
3. Create a SQL login and its user in `ErpAiPro` with `db_datareader` and `db_datawriter` for the api container, which only reads and writes rows, and a second login (`erp_local_migrator`) with `db_ddladmin` as well, for the `migrator` container that applies migrations and seeds before the api starts.
4. Write the git-ignored file `infra/compose/secrets/Erp__Platform__Database__ConnectionString` holding `Server=host.docker.internal,1433;Database=ErpAiPro;User ID=<login>;Password=<password>;Encrypt=True;TrustServerCertificate=True`. `compose.override.yaml` mounts it as the compose secret `Erp__Platform__Database__ConnectionString` on the api service, the key-per-file provider reads `/run/secrets/Erp__Platform__Database__ConnectionString` as `Erp:Platform:Database:ConnectionString`, and the compose smoke script refuses to start without the file, naming it.
5. Write the git-ignored file `infra/compose/secrets/Erp__Platform__Database__MigratorConnectionString` with the same shape for the migrator login. `compose.override.yaml` mounts it on the migrator service only, where it becomes `Erp:Platform:Database:MigratorConnectionString`; the api service starts only after the migrator exits with 0, and the smoke script checks that it did.
6. Write the git-ignored file `infra/compose/secrets/Erp__Platform__Attachments__EncryptionKey` holding the attachments encryption key, `<key id>:<32 random bytes in base64>`. From the repository root in Git Bash, `printf 'local:%s' "$(openssl rand -base64 32)" > infra/compose/secrets/Erp__Platform__Attachments__EncryptionKey` generates and writes it without displaying it. `compose.override.yaml` mounts it on the api service only (the migrator never needs it), where it becomes `Erp:Platform:Attachments:EncryptionKey`, and the compose smoke script refuses to start without it. Keep the file: attachments the stack stored cannot be read without the key they were stored under.

## Everyday commands

See the command table in the repository README. After changing an API route, request or response, refresh the contract and the web client with `node scripts/api-client/generate.ts` (it needs `ERP_TEST_SQL_CONNECTION` and `ERP_TEST_BLOB_EMULATOR_HOST`) and commit `docs/openapi/erp.json` with `frontend/packages/api-client/src/generated`. Before a pull request: `node scripts/verify/verify.ts` (it stops at once when either test variable is missing; add `--e2e` for Playwright, `--docker` for image builds and the compose smoke test).

## Commit signing

The `main` ruleset requires signed commits once signing is configured. SSH signing reuses the key already registered with GitHub:

```bash
git config --global gpg.format ssh
git config --global user.signingkey ~/.ssh/id_ed25519.pub
git config --global commit.gpgsign true
git config --global tag.gpgsign true
```

Add the same public key to GitHub under **Settings → SSH and GPG keys** with the key type **Signing Key** so GitHub shows commits as verified. `git log --show-signature -1` confirms a signature locally.

## Troubleshooting

- `dotnet test` reports "Testing with VSTest target is no longer supported": the solution runs on Microsoft.Testing.Platform; use the .NET 10 SDK from `global.json`.
- Tests fail with a message naming `ERP_TEST_SQL_CONNECTION`: set the variable as in "Configuration and secrets" and open a new terminal, because `setx` does not change the shell it runs in.
- Tests fail with a message naming `ERP_TEST_BLOB_EMULATOR_HOST`, or cannot reach `127.0.0.1:10000`: start Azurite and set the variable as in "Attachments storage" (`docker start erp-azurite` when the container already exists), then open a new terminal.
- The API stops at startup asking for exactly one of `Erp:Platform:Attachments:BlobServiceUri` and `Erp:Platform:Attachments:EmulatorHost`: without the store, set the endpoint in user secrets as in "First run"; when an emulator host is in user secrets, remove it with `cd backend && dotnet user-secrets remove "Erp:Platform:Attachments:EmulatorHost" --project Hosts/Api/Dewiride.Erp.Host.Api`.
- The API stops at startup naming `Erp:Platform:Attachments:EncryptionKey` (missing or not `<key id>:<32 random bytes in base64>`), or downloading an older attachment shows "not found" in its row after a key rotation (the API refuses a download link for content its configured keys cannot open, and logs an Error naming the content id): run the block in "Attachments storage" again, which copies the vault's current and retired keys into your user secrets.
- An upload of a file over 10 MB through the local containers' web origin (`http://localhost:3000`) never finishes: the standalone web server forwards only the first 10 MB and the request gets no answer (section "Containers"); upload through the API port or `pnpm dev`.
- Uploads or downloads fail right after you were given the storage role: role assignments take up to 10 minutes to reach storage; wait and retry, and check `az account show` names the Dewiride tenant.
- `dotnet ef` is not found: `cd backend && dotnet tool restore`.
- The compose smoke script stops naming a file under `infra/compose/secrets/`: create it as in "Containers".
- `pnpm install` refuses a package released less than 24 hours ago: `minimumReleaseAge` in `pnpm-workspace.yaml` is deliberate; wait or pin the previous version.
- Playwright browsers missing: `pnpm --filter @dewiride/erp-e2e exec playwright install --with-deps`.
