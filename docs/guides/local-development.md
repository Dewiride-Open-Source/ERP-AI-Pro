# Local development

## Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.0.x (band pinned in `backend/global.json`) | `dotnet --version` |
| Node.js | 24 LTS | `node --version` |
| pnpm | 12 | `npm i -g pnpm@12` then `pnpm --version` |
| Docker Desktop | current, with Compose v2 | `docker compose version` |
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
dotnet tool restore                                        # dotnet-ef from .config/dotnet-tools.json
dotnet build --no-restore -warnaserror
dotnet user-secrets set "Erp:Platform:Database:ConnectionString" "Server=localhost;Database=ErpAiPro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" --project Hosts/Api/Dewiride.Erp.Host.Api
dotnet ef database update --context SystemInfoDbContext --project Modules/Platform/SystemInfo/Module/Dewiride.Erp.Modules.Platform.SystemInfo --startup-project Hosts/Api/Dewiride.Erp.Host.Api   # creates ErpAiPro when it does not exist
dotnet test --solution Dewiride.Erp.slnx                   # needs ERP_TEST_SQL_CONNECTION (section "Configuration and secrets")
dotnet run --project Hosts/Api/Dewiride.Erp.Host.Api      # http://localhost:5080

# frontend (second terminal)
cd frontend
pnpm install --frozen-lockfile
pnpm dev                                                   # http://localhost:3000 → redirects to /login
```

`http://localhost:3000/platform/system-info` shows data fetched from the API through the `/api` rewrite. `http://localhost:5080/scalar` shows the API reference in Development.

## Configuration and secrets

- Non-secret defaults are in `backend/Hosts/Api/Dewiride.Erp.Host.Api/appsettings.json` and `appsettings.Development.json`.
- The API picks its configuration source at startup. With `APPCONFIG_ENDPOINT` set it loads Azure App Configuration with the `local-dev` label and resolves the Key Vault references from the local-dev vault (including the SQL Server connection string); without it a Development host runs on `appsettings.json` plus `dotnet user-secrets` and nothing in Azure is contacted. `dotnet ef` always runs the host without the store: the host sets `ERP_CONFIGURATION_SOURCE=LocalDevelopment` at EF design time, so migrations never depend on Azure.
- The database connection string has three homes on a developer machine, all Windows sign-in, none of them a committed file:
  - `dotnet run` with the store reads the Key Vault reference `Erp:Platform:Database:ConnectionString` (secret `Erp--Platform--Database--ConnectionString` in the local-dev vault, created with `az keyvault secret set --file` in the session that introduced the key, with the owner's consent).
  - `dotnet ef` and a store-less `dotnet run` read `dotnet user-secrets`: `cd backend && dotnet user-secrets set "Erp:Platform:Database:ConnectionString" "Server=localhost;Database=ErpAiPro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" --project Hosts/Api/Dewiride.Erp.Host.Api`.
  - Tests read the user environment variable `ERP_TEST_SQL_CONNECTION`, a server-level string without `Database=`: `setx ERP_TEST_SQL_CONNECTION "Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"`; `setx` writes the user environment, which only shells opened afterwards see.
- To use the store: `az login --tenant <tenant id>` once, then `cd backend && dotnet user-secrets set APPCONFIG_ENDPOINT https://<store>.azconfig.io --project Hosts/Api/Dewiride.Erp.Host.Api`. `ERP_ENVIRONMENT=local-dev` and `AZURE_TOKEN_CREDENTIALS=AzureCliCredential` (which makes `DefaultAzureCredential` use the `az login` session and nothing else) come from `Properties/launchSettings.json`; `APPCONFIG_ENDPOINT` itself is never committed.
- Any other machine-specific value goes to `dotnet user-secrets` (backend) and `frontend/apps/web/.env.local` (never committed). The web app validates its variables at startup (`src/shared/config/env.schema.ts`, documented in `frontend/apps/web/.env.example`): `API_INTERNAL_URL` defaults to `http://localhost:5080` in development and is mandatory when `NODE_ENV=production`; `NEXT_PUBLIC_APP_NAME` is inlined by `next build`, so changing it means rebuilding.
- Access to the store and the local-dev vault and the group to join are in [docs/operations/azure-bootstrap.md](../operations/azure-bootstrap.md), section "Developer onboarding".
- The refresh intervals under `Erp:Platform:Configuration` are read from `appsettings.json` before the store is connected; a store value never changes them.
- Integration tests never reach the store: every `ErpApiFactory` blanks `APPCONFIG_ENDPOINT` and forces the in-memory source, so the endpoint in your user secrets cannot switch a test host to Azure (a Development test host still loads the user-secrets file itself, exactly as `dotnet run` does). The `SqlTestDatabase` assembly fixture of every SQL-backed test project creates `ErpAiProTest_<yyyyMMddHHmmss>_<8 hex>` from `ERP_TEST_SQL_CONNECTION` (rights to create databases required), migrates it and drops it when the test process ends; `ErpApiFactory` points every test host at that database, so the user-secrets connection string never reaches a test.

## Containers

`docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml --profile observability up -d --wait` starts the API and web images plus the Aspire dashboard (`http://localhost:18888`) for local telemetry. No SQL Server container exists: the api container reaches the owner's SQL Server through `host.docker.internal` (Docker Desktop maps it to the host) with a SQL login, because a container cannot use Windows sign-in. Before the first `up`:

1. Enable TCP/IP for the instance on port 1433 (SQL Server Configuration Manager → SQL Server Network Configuration → Protocols → TCP/IP → Enabled, IPAll → TCP Port 1433) and restart the SQL Server service.
2. Enable mixed-mode sign-in (SQL Server Management Studio → server Properties → Security → SQL Server and Windows Authentication mode) and restart the service.
3. Create a SQL login and its user in `ErpAiPro` with `db_datareader`, `db_datawriter` and `db_ddladmin`.
4. Write the git-ignored file `infra/compose/secrets/Erp__Platform__Database__ConnectionString` holding `Server=host.docker.internal,1433;Database=ErpAiPro;User ID=<login>;Password=<password>;Encrypt=True;TrustServerCertificate=True`. `compose.override.yaml` mounts it as the compose secret `Erp__Platform__Database__ConnectionString` on the api service, the key-per-file provider reads `/run/secrets/Erp__Platform__Database__ConnectionString` as `Erp:Platform:Database:ConnectionString`, and the compose smoke script refuses to start without the file, naming it.

## Everyday commands

See the command table in the repository README. Before a pull request: `node scripts/verify/verify.ts` (add `--e2e` for Playwright, `--docker` for image builds and the compose smoke test).

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
- `dotnet ef` is not found: `cd backend && dotnet tool restore`.
- The compose smoke script stops naming `infra/compose/secrets/Erp__Platform__Database__ConnectionString`: create the file as in "Containers".
- `pnpm install` refuses a package released less than 24 hours ago: `minimumReleaseAge` in `pnpm-workspace.yaml` is deliberate; wait or pin the previous version.
- Playwright browsers missing: `pnpm --filter @dewiride/erp-e2e exec playwright install --with-deps`.
