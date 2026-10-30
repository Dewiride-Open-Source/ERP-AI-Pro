# ADR-0055: The production host restarts an unhealthy container and alerts nobody

Status: accepted
Date: 2026-10-30

## Context

- **What existed.** Each image carries a Docker health check: the api runs the HealthProbe host, the web server answers its own `/healthz`, and Caddy answers its admin endpoint. Compose starts a service only once those it depends on are healthy. `erp-ai-pro.service` and `deploy.sh` wait for every container's health ([ADR-0049](0049-the-production-host-a-deploy-account-keys-from-the-home-network-and-nightly-updates.md), [ADR-0053](0053-the-release-public-images-a-dependabot-gate-attestations-verified-on-the-server-and-the-deployment-scripts.md)). The containers run with `restart: unless-stopped`. The roadmap item planned an external uptime monitor that would hit `/healthz/live` through the proxy and alert the owner, and a `/api/health` route in the web app.
- **Facts that shaped the design** (official sources, read 2026-10-28 to 2026-10-30).
  - A restart policy acts when a container exits or stops: "Restart the container if it exits due to an error" (`on-failure`), "Always restart the container if it stops" (`always`, which `unless-stopped` follows). A health check only changes the container's status: "It takes retries consecutive failures of the health check for the container to be considered unhealthy", and "a health_status event is generated with the new status". Docker neither stops nor restarts an unhealthy container (docs.docker.com, "Start containers automatically" and the Dockerfile reference, `HEALTHCHECK`).
  - The server is reachable only from the home network ([ADR-0049](0049-the-production-host-a-deploy-account-keys-from-the-home-network-and-nightly-updates.md), [ADR-0052](0052-the-edge-proxys-tls-policy-limits-timeouts-headers-and-access-log.md)), so no probe on the internet can reach it.
  - A web route at `/api/health` could never be reached, because the proxy sends all of `/api/*` to the api. Through the proxy, `/healthz/live` and `/healthz/ready` reach the web container, and `deploy.sh` already reads the API's readiness from inside the proxy container.
  - Readiness runs every check on every request (one database connection per catalogue context and one storage call) and is exempt from rate limiting ([ADR-0019](0019-rate-limiting-reference-data-caching-and-health.md)), so a proxy route to it would let any device on the home network load the database.
- **Owner decisions of 2026-10-30.** "dont want to here if server active/failing": nothing notifies anyone whether the server is up or failing. A part of the ERP that freezes is restarted by itself ("Restart it, tell me", which with no notification means a line in the server's journal).

## Decision

- **No alerting.** No uptime monitor, heartbeat, Azure Monitor workspace, alert rule, action group or notification channel exists. An outage is seen when someone opens the ERP. The server keeps its own record of its health: `systemctl status erp-ai-pro.service`, the journals of `erp-ai-pro.service`, `erp-ai-pro-retain.service` and `erp-ai-pro-heal.service`, and `docker compose ps`.
- **Self-repair.** `erp-ai-pro-heal.timer` runs `erp-ai-pro-heal.service` every minute as `erp-ai-pro` in the compose folder, and the service runs `scripts/deploy/heal.sh` of the deployment checkout, which:
  - lists the running containers of the compose project `erp-ai-pro` that Docker reports `unhealthy`;
  - restarts each one that started at least 30 minutes ago, with `docker restart`, which keeps the container, and leaves one that started later alone, a deployment's or a boot's own start included, so a container is restarted at most every half hour;
  - never starts a stopped container, because a stop is someone's decision or the shutdown's;
  - takes `/srv/erp-ai-pro/deploy.lock` without waiting, so it never acts while a deployment, a rollback, the nightly `retain` or the start at boot holds it, and those wait for it only the seconds it takes;
  - writes one journal line for each container it restarts (a warning) or leaves alone, nothing when the stack is healthy, and exits 1 when a restart fails or a start time cannot be read, after handling the other containers.
- **The health checks and routes stay as they are.** The proxy gains no route to the API's probes, and the web app no `/api/health`.
- **The installation.** `bootstrap.sh` installs, enables and starts both units with the others. `check.sh` checks them: the account, the folder, the `.env` condition, the script the service runs, no unit it starts, and a timer of every minute. `tests/heal-decisions.sh` proves the script's decisions with stand-in commands, and the bootstrap runner test proves the units on fresh Ubuntu 22.04, 24.04 and 26.04 runners.
- **Earlier decisions this changes.**
  - ADR-0049: a third pair of units joins the boot and nightly units.
  - ADR-0053: the heal service takes the deployment lock too, without waiting, and the scripts' lock messages name it.

## Options rejected

- **Telling the owner.** A watchdog reporting to Azure Monitor through the Logs Ingestion API with a log search alert by email and the Azure app (about ₹72 a month, or about ₹216 to be told within 10 to 15 minutes), Azure Arc with the Azure Monitor Agent (free, but two Microsoft programs with full control of the server and a remote-management link), and the ERP's own telemetry in Application Insights. The owner wants no notification.
- **An auto-heal container.** A third-party image, not approved, that would hold the Docker socket and so be root on the host.
- **A long-running listener on `docker events`.** It reacts sooner, but needs its own reconnect and restart handling, which a timer of one minute does not.
- **Recreating the container** (`docker compose up --force-recreate`). It replaces the container, and without `--no-deps` it runs the migrator again (ADR-0053); a restart is all a frozen process needs.
- **A proxy route to `/healthz/ready`.** Nobody would read it, and every request would run every check.

## Consequences

- **People notice an outage.** Nothing reports a stopped server, a failed start at boot, a failed nightly `retain` or a restart. The journal is where to learn what happened.
- **A part that freezes comes back on its own.** One that has run 30 minutes is restarted within about two minutes of freezing: Docker marks it unhealthy after three failed checks 30 seconds apart, and the next minute's run restarts it. One that freezes again sooner waits until it has run 30 minutes, and one that keeps freezing restarts every half hour without escalating.
- **The heal service holds the lock for seconds each minute.** `deploy.sh` and `rollback.sh` wait for it, as they wait up to 14 minutes for any holder.
- **Before the first deployment** the service finds no container and succeeds.
