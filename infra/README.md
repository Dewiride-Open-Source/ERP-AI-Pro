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
| `compose/compose.override.yaml` | local development additions (merged automatically): ports 5080/3000 published on loopback only, `host.docker.internal` for the owner's SQL Server, the data-only and migrator connection secrets, Aspire dashboard under the `observability` profile |
| `compose/compose.production.yaml` | on-premises hardening: read-only root filesystem, dropped capabilities, `no-new-privileges`, resource limits, log rotation |
| `compose/.env.example` | non-secret interpolation values; copy to `.env` (git-ignored) |

There is no SQL Server container: local development uses the owner's SQL Server instance and production uses Azure SQL.

```bash
# local: build and run both images with the Aspire dashboard
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml --profile observability up -d --build --wait

# production (owner, on the Ubuntu host): pull, migrate on its own, then roll the stack (docs/operations/runbooks/migrations.md)
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml pull api web
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml run --rm migrator
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml up -d --remove-orphans --wait
```

The migrator runs on its own first because `up` replaces a changed `api` container before it waits for the migrator; running it separately keeps the previous API serving when a migration fails ([ADR-0021](../docs/adr/0021-migrator-program-and-seeding.md)). The edge reverse proxy and TLS arrive with the first-deployment phase.
