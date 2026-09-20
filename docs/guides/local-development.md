# Local development

## Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.0.x (band pinned in `backend/global.json`) | `dotnet --version` |
| Node.js | 24 LTS | `node --version` |
| pnpm | 12 | `npm i -g pnpm@12` then `pnpm --version` |
| Docker Desktop | current, with Compose v2 | `docker compose version` |
| Azure CLI | current, with Bicep (`az bicep install`), signed in to the Dewiride tenant | `az account show`, `az bicep version` |
| SQL Server | the owner's instance, reachable from this machine | SQL Server Management Studio |
| Git | `core.longpaths true` on Windows | `git config core.longpaths` |

## First run

```bash
git clone https://github.com/Dewiride-Open-Source/ERP-AI-Pro.git
cd ERP-AI-Pro

# backend
cd backend
dotnet restore
dotnet build --no-restore -warnaserror
dotnet test --solution Dewiride.Erp.slnx
dotnet run --project Hosts/Api/Dewiride.Erp.Host.Api      # http://localhost:5080

# frontend (second terminal)
cd frontend
pnpm install --frozen-lockfile
pnpm dev                                                   # http://localhost:3000 → redirects to /login
```

`http://localhost:3000/platform/system-info` shows data fetched from the API through the `/api` rewrite. `http://localhost:5080/scalar` shows the API reference in Development.

## Configuration and secrets

- Non-secret defaults are in `backend/Hosts/Api/Dewiride.Erp.Host.Api/appsettings.json` and `appsettings.Development.json`.
- Once the Azure configuration phase is complete, `az login` plus `APPCONFIG_ENDPOINT` and `ERP_ENVIRONMENT=local-dev` load the rest from Azure App Configuration and the local-dev Key Vault (including the SQL Server connection string).
- Until then, machine-specific values go to `dotnet user-secrets` (backend) and `frontend/apps/web/.env.local` (never committed).
- Access to the store and the local-dev vault, the group to join and the `dotnet user-secrets` command for `APPCONFIG_ENDPOINT` are in [docs/operations/azure-bootstrap.md](../operations/azure-bootstrap.md), section "Developer onboarding".
- Integration tests that need a database read `ERP_TEST_SQL_CONNECTION` (a server-level connection with rights to create databases) and create a fresh database per run.

## Containers

`docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml --profile observability up -d --wait` starts the API and web images plus the Aspire dashboard (`http://localhost:18888`) for local telemetry. No SQL Server container exists; containers reach the owner's SQL Server through `host.docker.internal` once the connection string is configured.

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
- `pnpm install` refuses a package released less than 24 hours ago: `minimumReleaseAge` in `pnpm-workspace.yaml` is deliberate; wait or pin the previous version.
- Playwright browsers missing: `pnpm --filter @dewiride/erp-e2e exec playwright install --with-deps`.
