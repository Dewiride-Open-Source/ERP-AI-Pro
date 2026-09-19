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
| `compose/compose.yaml` | base stack: `api` and `web` on an internal network; nothing publishes ports |
| `compose/compose.override.yaml` | local development additions (merged automatically): ports 5080/3000 published on loopback only, `host.docker.internal` for the owner's SQL Server, Aspire dashboard under the `observability` profile |
| `compose/compose.production.yaml` | on-premises hardening: read-only root filesystem, dropped capabilities, `no-new-privileges`, resource limits, log rotation |
| `compose/.env.example` | non-secret interpolation values; copy to `.env` (git-ignored) |

There is no SQL Server container: local development uses the owner's SQL Server instance and production uses Azure SQL.

```bash
# local: build and run both images with the Aspire dashboard
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml --profile observability up -d --build --wait

# production (owner, on the Ubuntu host)
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml up -d --pull always --remove-orphans --wait
```

The edge reverse proxy, TLS and the migrator service arrive with the first-deployment phase.
