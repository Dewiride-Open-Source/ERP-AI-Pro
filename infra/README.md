# Infrastructure

## Images

| Image | Dockerfile | Base | Notes |
|---|---|---|---|
| `ghcr.io/dewiride-open-source/erp-ai-pro/api` | `docker/api.Dockerfile` | `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra` | non-root `app` user, port 8080, health probe console under `/app/probe` because the chiseled image has no shell |
| `ghcr.io/dewiride-open-source/erp-ai-pro/web` | `docker/web.Dockerfile` | `node:24-slim` | Next.js standalone output, non-root `node` user, port 3000 |
| `ghcr.io/dewiride-open-source/erp-ai-pro/proxy` | `docker/proxy.Dockerfile` | `caddy:2.11.7-alpine` | the edge proxy, `proxy/Caddyfile`, non-root `caddy` user, ports 80 and 443 ([ADR-0048](../docs/adr/0048-the-edge-proxy-caddy-on-the-servers-local-address.md)); its TLS policy, body limits, timeouts, headers and access log ([ADR-0052](../docs/adr/0052-the-edge-proxys-tls-policy-limits-timeouts-headers-and-access-log.md)) |

The Dockerfiles use the repository root as build context and pin their base images by digest; Dependabot keeps the digests current.

## Compose

| File | Purpose |
|---|---|
| `compose/compose.yaml` | base stack: the one-shot `migrator` (the api image running `Hosts/Migrator`), `api` (starts only after the migrator exits with 0), `web` and `proxy` (starts once api and web are healthy) on an internal network; the overlays below publish the ports, and in production only the proxy's |
| `compose/compose.override.yaml` | local development additions (merged automatically): the proxy on `127.0.0.1:8080` and `127.0.0.1:8443`, ports 5080/3000 published on loopback only, `host.docker.internal` for the owner's SQL Server, the data-only and migrator connection secrets, the attachments encryption-key secret, the `azurite` storage emulator the api keeps attachments in, Aspire dashboard under the `observability` profile |
| `compose/compose.production.yaml` | on-premises hardening: read-only root filesystem, dropped capabilities, `no-new-privileges`, resource limits, every service's lines in the server's journal under its tag `erp-ai-pro-<service>` ([ADR-0058](../docs/adr/0058-records-kept-a-year-inside-india.md)); the proxy's 80 and 443 on IPv4 only; the external volume `erp-ai-pro-data-protection-keys` on the api, which holds the Data Protection key ring that keeps people signed in across restarts ([ADR-0032](../docs/adr/0032-token-cache-in-sql-server-and-the-data-protection-key-ring.md)), and `erp-ai-pro-proxy-data` on the proxy, which holds its certificate authority |
| `compose/compose.backup.yaml` | the project `erp-ai-pro-backup`, a separate file that resolves without the release's values, so a rebuilt server can restore before its first deployment: for each external volume (`erp-ai-pro-data-protection-keys`, `erp-ai-pro-proxy-data`) a BusyBox reader (read-only) and writer, both running as the volume's owner, and an owner service running as root with `CHOWN` and `FOWNER` alone, all on Microsoft's distroless debug image and without network; and the Azure CLI image as 1654 with `secrets/erp-backup-client.pem` as its one secret. Every image is digest-pinned, every service has a read-only root, `cap_drop: [ALL]`, `no-new-privileges` and no container log, and only `scripts/deploy/backup.sh` and `restore.sh` run them, through `docker compose run` ([ADR-0056](../docs/adr/0056-the-servers-own-files-backed-up-nightly-sealed-to-an-operator-key-and-the-restore-drill.md)) |
| `compose/.env.example` | non-secret interpolation values; copy to `.env` (git-ignored) |

There is no SQL Server container: local development uses the owner's SQL Server instance and production uses Azure SQL.

The local stack keeps attachments in the `azurite` service, Microsoft's Blob Storage emulator (`mcr.microsoft.com/azure-storage/azurite`, pinned by digest and bumped by Dependabot): blob service only, data in the named volume `azurite-data`, telemetry off, no port published to the host, healthy once port 10000 accepts connections. The api service reaches it as `azurite` through `Erp__Platform__Attachments__EmulatorHost` and starts only once it is healthy. Production keeps attachments in the Entra-only Azure Storage account `sterpaiproprod`, which answers only the home's public address ([ADR-0051](../docs/adr/0051-the-production-attachments-storage-geo-redundant-and-reachable-from-the-homes-address-alone.md)) and whose endpoint and key reach the api through App Configuration, and `compose.production.yaml` has no emulator.

```bash
# local: build and run both images with the Aspire dashboard
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml --profile observability up -d --build --wait

# production (as erp-ai-pro on the host, which scripts/deploy/bootstrap.sh prepares): verify the release, pull its
# images by digest, migrate on its own, then roll the stack (docs/operations/runbooks/deploy.md)
bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/deploy.sh v0.1.0
```

In production `compose.production.yaml` runs every image as `<image>:${IMAGE_TAG}@<digest>`, from the four release values `deploy.sh` writes into `.env`, and refuses to resolve without them ([ADR-0053](../docs/adr/0053-the-release-public-images-a-dependabot-gate-attestations-verified-on-the-server-and-the-deployment-scripts.md)). The migrator runs on its own first because `up` replaces a changed `api` container before it waits for the migrator; running it separately keeps the previous API serving when a migration fails ([ADR-0021](../docs/adr/0021-migrator-program-and-seeding.md)). Its `migrate` also deletes the security events and change records past their retention, and `retain` does only that, so the nightly run that `erp-ai-pro-retain.timer` starts on the host moves no schema ahead of the running API ([ADR-0043](../docs/adr/0043-change-records-and-the-append-only-audit-schema.md)). Both volumes are declared `external`, so `up` refuses to start until they exist and `docker compose down -v` can never delete them; the host bootstrap creates them. The api image creates the key ring's mount point owned by `app` with mode 0700, which an empty volume takes on at its first mount, and losing that volume signs everyone out ([secrets runbook](../docs/operations/runbooks/secrets.md), section 5e).

## Host

`host/` holds what `scripts/deploy/bootstrap.sh` installs on the production server ([ADR-0049](../docs/adr/0049-the-production-host-a-deploy-account-keys-from-the-home-network-and-nightly-updates.md), [host setup runbook](../docs/operations/runbooks/host-setup.md)):

| File | Installed as | Does |
|---|---|---|
| `host/systemd/erp-ai-pro.service` | `/etc/systemd/system/` | brings the last deployment's api, web and proxy containers back at boot, never running the migrator, recreating a container or pulling, and tries a failed start again every minute; it waits for the deployment lock `/srv/erp-ai-pro/deploy.lock` |
| `host/systemd/erp-ai-pro-retain.service`, `.timer` | `/etc/systemd/system/` | `run --rm -T migrator retain` every night at 01:00 India time, under the same lock, its result in the journal |
| `host/systemd/erp-ai-pro-heal.service`, `.timer` | `/etc/systemd/system/` | `scripts/deploy/heal.sh` every minute: restarts a running container that Docker reports unhealthy once it has run 30 minutes, never starts a stopped one, takes the same lock without waiting and writes each restart to the journal; nothing alerts anyone ([ADR-0055](../docs/adr/0055-the-production-host-restarts-an-unhealthy-container-and-alerts-nobody.md)) |
| `host/systemd/erp-ai-pro-backup.service`, `.timer` | `/etc/systemd/system/` | `scripts/deploy/backup.sh` every night at 02:00 India time, as `erp-ai-pro`: seals the key ring and proxy volumes, `.env` and `.env.previous` to the backup encryption certificate and uploads them to the write-once container `host-backups`; it holds the same lock only while it copies the files, waiting at most 14 minutes for it; each step is a line of its journal, and nothing alerts anyone ([ADR-0056](../docs/adr/0056-the-servers-own-files-backed-up-nightly-sealed-to-an-operator-key-and-the-restore-drill.md), [restore runbook](../docs/operations/runbooks/restore.md)) |
| `host/ssh/10-erp-ai-pro.conf` | `/etc/ssh/sshd_config.d/` | public keys only, no root sign-in, no X11 forwarding |
| `host/apt/52erp-ai-pro-unattended-upgrades.conf` | `/etc/apt/apt.conf.d/` | Ubuntu's security and regular updates every day, the GitHub CLI's repository included, restarting the server at 03:00 India time when one needs it |
| `host/systemd/erp-ai-pro-journal.service`, `.timer` | `/etc/systemd/system/` | `scripts/deploy/journal.sh` every night at 02:30 India time, as `erp-ai-pro` with the group `systemd-journal`: seals each India day of the journal not copied yet to the backup encryption certificate and uploads it to the write-once container `host-journal`, catching up the last 30 days ([ADR-0058](../docs/adr/0058-records-kept-a-year-inside-india.md), [records runbook](../docs/operations/runbooks/records.md)) |
| `host/systemd/docker.service.d/50-erp-ai-pro.conf` | `/etc/systemd/system/docker.service.d/` | lets `docker.service`, through which every container logs, write 100,000 lines per 30 seconds to the journal instead of 10,000, so none is dropped |
| `host/journald/50-erp-ai-pro.conf` | `/etc/systemd/journald.conf.d/` | a persistent, compressed journal of at most 24 GB in 400 files, 20 GB of the disk kept free, each entry kept more than a year (files deleted 400 days after their first entry) |
| `host/docker/daemon.json` | `/etc/docker/daemon.json` | live restore, and the journal as the log of containers without logging of their own |

### Local secret files

Git-ignored files under `compose/secrets/`, mounted as compose secrets and read by the key-per-file provider from `/run/secrets/<name>`; [docs/guides/local-development.md](../docs/guides/local-development.md), section "Containers", shows how to create each one. The compose smoke script refuses to start while one is missing.

| File | Service | Holds |
|---|---|---|
| `Erp__Platform__Database__ConnectionString` | `api` | connection string of the data-only SQL login (`erp_local`: `db_datareader`, `db_datawriter` and the role `erp_audit_append_only`, so it may only insert into the schema `audit`) |
| `Erp__Platform__Database__MigratorConnectionString` | `migrator` | connection string of the SQL login that may also change the schema (`erp_local_migrator`: `db_ddladmin`, `db_datareader`, `db_datawriter`, the database permission `CREATE ROLE` and `CONTROL` on the schema `audit`, never `db_owner`), which also deletes what is past its retention |
| `Erp__Platform__Attachments__EncryptionKey` | `api` | the attachments encryption key, `<key id>:<32 random bytes in base64>`, generated on the machine and never displayed |
