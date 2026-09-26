# Infrastructure

## Images

| Image | Dockerfile | Base | Notes |
|---|---|---|---|
| `ghcr.io/dewiride-open-source/erp-ai-pro/api` | `docker/api.Dockerfile` | `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra` | non-root `app` user, port 8080, health probe console under `/app/probe` because the chiseled image has no shell |
| `ghcr.io/dewiride-open-source/erp-ai-pro/web` | `docker/web.Dockerfile` | `node:24-slim` | Next.js standalone output, non-root `node` user, port 3000 |

Both Dockerfiles use the repository root as build context and pin their base images by digest; Dependabot keeps the digests current.

## Compose

| File | Purpose |
|---|---|
| `compose/compose.yaml` | base stack: the one-shot `migrator` (the api image running `Hosts/Migrator`), `api` (starts only after the migrator exits with 0) and `web` on an internal network; nothing publishes ports |
| `compose/compose.override.yaml` | local development additions (merged automatically): ports 5080/3000 published on loopback only, `host.docker.internal` for the owner's SQL Server, the data-only and migrator connection secrets, the attachments encryption-key secret, the `azurite` storage emulator the api keeps attachments in, Aspire dashboard under the `observability` profile |
| `compose/compose.production.yaml` | on-premises hardening: read-only root filesystem, dropped capabilities, `no-new-privileges`, resource limits, log rotation |
| `compose/.env.example` | non-secret interpolation values; copy to `.env` (git-ignored) |

There is no SQL Server container: local development uses the owner's SQL Server instance and production uses Azure SQL.

The local stack keeps attachments in the `azurite` service, Microsoft's Blob Storage emulator (`mcr.microsoft.com/azure-storage/azurite`, pinned by digest and bumped by Dependabot): blob service only, data in the named volume `azurite-data`, telemetry off, no port published to the host, healthy once port 10000 accepts connections. The api service reaches it as `azurite` through `Erp__Platform__Attachments__EmulatorHost` and starts only once it is healthy. Production keeps attachments in an Entra-only Azure Storage account (created in the first-deployment phase), and `compose.production.yaml` has no emulator.

```bash
# local: build and run both images with the Aspire dashboard
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml --profile observability up -d --build --wait

# production (owner, on the Ubuntu host): pull, migrate on its own, then roll the stack (docs/operations/runbooks/migrations.md)
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml pull api web
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml run --rm migrator
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml up -d --remove-orphans --wait
```

The migrator runs on its own first because `up` replaces a changed `api` container before it waits for the migrator; running it separately keeps the previous API serving when a migration fails ([ADR-0021](../docs/adr/0021-migrator-program-and-seeding.md)). The edge reverse proxy and TLS arrive with the first-deployment phase.

### Local secret files

Git-ignored files under `compose/secrets/`, mounted as compose secrets and read by the key-per-file provider from `/run/secrets/<name>`; [docs/guides/local-development.md](../docs/guides/local-development.md), section "Containers", shows how to create each one. The compose smoke script refuses to start while one is missing.

| File | Service | Holds |
|---|---|---|
| `Erp__Platform__Database__ConnectionString` | `api` | connection string of the data-only SQL login (`db_datareader`, `db_datawriter`) |
| `Erp__Platform__Database__MigratorConnectionString` | `migrator` | connection string of the SQL login that may also change the schema (`db_ddladmin`) |
| `Erp__Platform__Attachments__EncryptionKey` | `api` | the attachments encryption key, `<key id>:<32 random bytes in base64>`, generated on the machine and never displayed |
