# ADR-0049: The production host: a deploy account, keys from the home network, nightly updates

Status: accepted
Date: 2026-10-18

## Context

- **What existed.**
  - The production stack is Docker Compose: `compose.yaml` with `compose.production.yaml` runs the migrator, api, web and proxy containers. Every container runs non-root, read-only, with every capability dropped (ADR-0048).
  - The api reads its client certificates from the compose file secrets `erp-runtime-client.pem` and `erp-migrator-client.pem`, owned by the image user 1654 with mode 0400 (`docs/operations/runbooks/secrets.md`).
  - Two external volumes outlive every `docker compose down -v`: `erp-ai-pro-data-protection-keys` (ADR-0032) and `erp-ai-pro-proxy-data`. The second holds the proxy's root key, which every trusting device accepts for any name (residual risk 16).
  - The migrator's `retain` command deletes what is past its retention without migrating (ADR-0043), and nothing ran it between deployments.
- **The owner's server, measured on 2026-10-18.**
  - A bare-metal Ubuntu 26.04.1 LTS with its clock in UTC, Docker 29.9.1 and Compose 5.6.0 from Docker's apt repository, and sign-in through the socket-activated `ssh.socket`.
  - Its wired IPv4 address on the home network comes from the router's DHCP, and SLAAC gives it a global IPv6 address from the internet provider's prefix.
  - `ufw` was installed but off, password sign-in was allowed (Ubuntu's default, with an installer's `50-cloud-init.conf` in `sshd_config.d`), and the administrator's account was a member of `docker`.
  - An unused WordPress stack ran there; the owner had it removed on 2026-10-18.
- **Owner decisions of 2026-10-18.**
  - The host restarts itself at 03:00 India time when an update needs it.
  - SSH accepts keys only, from the home network only.
  - The lockout tool fail2ban, which the roadmap scope named, is declined.
- **What the tools document.**
  - Docker publishes a port without an address "to all host addresses (`0.0.0.0` and `[::]`)". On the host's Docker 29.9.1, `-p 0.0.0.0:18080:80` bound IPv4 only, and `-p 18081:80` bound both (measured).
  - "When you expose container ports using Docker, these ports bypass your firewall rules" (Docker's Ubuntu install guide), and "the docker group grants root-level privileges to the user" (Linux post-installation steps).
  - Live restore keeps containers running while the daemon restarts, across patch releases. `live-restore` is applied by a configuration reload, and `log-opts` only by a restart (`dockerd` reference, "Configuration reload behavior").
  - For each keyword "the first obtained value will be used", and `Include` expands its glob "in lexical order" (`sshd_config(5)`).
  - Unattended upgrades ship with the official Ubuntu origins only; `Automatic-Reboot` defaults to false, and an `hh:mm` `Automatic-Reboot-Time` is "in the local system's timezone". needrestart restarts affected services by itself since Ubuntu 24.04, and Ubuntu's `needrestart.conf` excludes `docker` (Ubuntu Server, "Automatic updates", read on the host).
  - A unit in `Requires=` that is explicitly stopped or restarted stops or restarts its dependents, while `Wants=` does not. A failed `ConditionPathExists=` skips a unit without failing it (`systemd.unit(5)`). A calendar event may name an IANA time zone (`systemd.time(7)`).

## Decision

- **`scripts/deploy/bootstrap.sh`.**
  - The administrator runs it once with sudo from their own account, from a checkout of this repository: `--site-address <the fixed IPv4 address>`, `--authorized-keys <file>`, and optionally `--home-network <CIDR>`, which defaults to the site address's network.
  - Every step compares, then changes only what differs, and a second run prints `nothing to change: the host already matches`.
  - `--dry-run` changes nothing. Without root it compares what it can read and lists the rest as changes for a run with sudo to compare.
  - It refuses Ubuntu other than 22.04, 24.04 or 26.04 LTS, a clock other than UTC, a site address that is not the host's, and a keys file with a line that is not a public key.
- **The deploy account `erp-ai-pro`.**
  - Home `/srv/erp-ai-pro` (0750); no usable password (`*`); its `authorized_keys` is the given file.
  - It is the only member of `docker`: the bootstrap removes every other member, the administrator included, who from then on runs Docker through sudo. It refuses a host where `docker` is someone's primary group.
- **The deployment checkout.**
  - `/srv/erp-ai-pro/ERP-AI-Pro` is cloned from the checkout the bootstrap runs from, at its commit, with that checkout's `origin` and owned by the deploy account. Deployments move it; the bootstrap never does again.
  - It holds `infra/compose/secrets` (0700, the deploy account's) for the two client certificates. The administrator installs them with sudo, owned by 1654 with mode 0400.
  - `infra/compose/.env` (0600) is copied once from `.env.example` with `ERP_SITE_ADDRESS` filled in. A later run only warns when the address differs, because the address changes together with the production sign-in settings (ADR-0048).
  - The bootstrap creates both external volumes when they are missing.
- **Docker.**
  - Docker Engine is installed from Docker's repository as its Ubuntu guide describes, unless a Docker Engine with the Compose plugin is already there. It refuses the unofficial packages that guide asks to remove.
  - `/etc/docker/daemon.json` (`infra/host/docker/daemon.json`) turns on live restore and rotates the logs of containers that set no logging of their own (20 MB, 5 files). The bootstrap applies it with a reload, which applies live restore, then a restart, which keeps running containers. It refuses to merge another `daemon.json`.
  - Docker's packages come from Docker's repository, which unattended upgrades does not read, so they are updated by hand (`docs/operations/runbooks/host-setup.md`).
- **The ERP's ports are IPv4 only.** `compose.production.yaml` publishes the proxy's 80 and 443 on `0.0.0.0`. The router's address translation keeps the IPv4 ports inside the home network, since nothing is forwarded, and the global IPv6 address reaches no published port.
- **SSH.**
  - `infra/host/ssh/10-erp-ai-pro.conf` sorts before every other file of `sshd_config.d`. It sets `PasswordAuthentication no`, `KbdInteractiveAuthentication no`, `AuthenticationMethods publickey`, `PermitRootLogin no` and `X11Forwarding no`.
  - The bootstrap first checks that the administrator has a usable key in `authorized_keys`. It tests the configuration with `sshd -t`, putting the previous rules back if that fails, then reloads `ssh.service` and proves the effective values with `sshd -T`.
- **The firewall.** ufw denies new incoming connections by default, allows outgoing ones, and admits SSH over IPv4 from the home network only; nothing is admitted over IPv6. Before turning the firewall on, the bootstrap refuses when its own SSH session comes from outside the home network or over IPv6.
- **Nightly updates.**
  - `infra/host/apt/52erp-ai-pro-unattended-upgrades` keeps the daily list update and upgrade on. It sets `Automatic-Reboot "true"` and `Automatic-Reboot-Time "21:30"`, which is 03:00 India time on a UTC clock.
  - The bootstrap unmasks and enables `apt-daily.timer` and `apt-daily-upgrade.timer` when needed, and proves the settings with `apt-config dump`.
- **systemd** (`infra/host/systemd`).
  - `erp-ai-pro.service` (oneshot, `RemainAfterExit`, `User=erp-ai-pro`) brings the last deployment's containers back at boot: `docker compose … up --detach --wait --no-build --no-recreate --pull never --no-deps api web proxy`. It never runs the migrator, never replaces a container and never pulls. `ExecStop` stops the stack in order at shutdown. It `Wants=` docker.service, so a Docker update's restart leaves the stack to live restore.
  - `erp-ai-pro-retain.timer` runs `erp-ai-pro-retain.service`, `docker compose … run --rm -T migrator retain`, at 01:00 India time with up to 15 minutes' delay. It is `Persistent`, so a run the host slept through happens at the next boot. Its output and exit code stay in the journal.
  - Both services wait, skipped rather than failed, for `.env` and their client certificates.
- **Verification.**
  - `scripts/deploy/check.sh` runs in `verify.ts` and CI. It syntax-checks the deploy scripts and shellchecks them, checks that the units name the deploy account, the checkout and only the stack's files, and checks that the daemon configuration is JSON. Where the tools exist, it runs `systemd-analyze verify`, `apt-config` and `dockerd --validate`.
  - The `docker-build` job `Bootstrap a fresh Ubuntu host` (`scripts/deploy/tests/bootstrap-runner.sh`) runs a dry run without root and two runs with sudo on a GitHub-hosted Ubuntu runner, then proves the result:
    - the second run changes nothing;
    - the deploy account signs in with its key and reaches Docker with live restore;
    - a password sign-in is refused;
    - `sshd -T`, the firewall, the docker group, the units, the APT settings, the checkout with its secrets folder and `.env`, and the volumes are as decided;
    - the production configuration publishes 80 and 443 on `0.0.0.0` only.

## Options rejected

- **fail2ban.** With passwords off, there is nothing to guess. With SSH admitted from the home network only, no one outside can try. The package comes from Ubuntu's community-maintained `universe`, and a ban by address could lock out the administrator's own computer after a mistake. The owner declined it.
- **The administrator in `docker`.** Anyone who signs in as the administrator, with any key on that account, would be root without the password. They could read the proxy's root key, the client certificates and the key ring.
- **Docker's packages in unattended upgrades.** Docker's repository has no security pocket, so every release, minor and major alike, would install unattended in the middle of the day, and live restore covers patch releases only.
- **A boot unit that runs `docker compose up --wait` alone.** It would run the migrator's seeders and sweeps at every boot. After a deployment interrupted between pulling and migrating, it would start a new api against a schema without its migrations.
- **`Requires=docker.service`.** Restarting Docker for an update would then stop and restart the whole stack.
- **Rules in Docker's `DOCKER-USER` chain** to keep 80 and 443 to the home network. They would live outside ufw and need persisting of their own, while the router's address translation already keeps the IPv4 ports in, and IPv6 publishes none.
- **A static address set on the host.** The router's DHCP could hand the same address to another device. A reservation in the router keeps one owner of the address.
- **Rootless Docker.** It changes where volumes live and how ports below 1024 are bound. The stack already runs every container non-root with every capability dropped.

## Consequences

- **The administrator.** They run Docker through sudo, or as `erp-ai-pro` (`sudo -iu erp-ai-pro`). Signing in over the network needs a key; the console still takes the password.
- **The nightly restart.** On a night when an update needs a restart, the ERP is off for about two minutes after 03:00 India time and comes back through the containers' restart policy and `erp-ai-pro.service`.
- **Retention.** Once the certificates are in place, security events and change records past their retention are deleted every night. That narrows residual risk 14 to a timer that stops or a run that fails, whose result the journal keeps.
- **The checkout's files.** It holds `.env` and the certificates, which git ignores, so `git clean -x` there deletes them. Deployments (`first-deployment-release-and-deploy-pipeline`) sign in as `erp-ai-pro` and move the checkout to a release.
- **The units start working at go-live.** They do nothing until the client certificates are installed (`first-deployment-production-database-provisioning`, `first-deployment-go-live-checklist-and-production-smoke`).
- **The router.** It must reserve the address for the host's network adapter and forward no port.
