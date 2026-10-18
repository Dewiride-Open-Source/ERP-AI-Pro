# ADR-0049: The production host: a deploy account, keys from the home network, nightly updates

Status: accepted
Date: 2026-10-18

## Context

- **What existed.**
  - The production stack is Docker Compose: `compose.yaml` with `compose.production.yaml` runs the migrator, api, web and proxy containers. Every container runs non-root, read-only, with every capability dropped (ADR-0048).
  - The api and the migrator each read their own client certificate from a compose file secret, `erp-runtime-client.pem` and `erp-migrator-client.pem`, owned by the image user 1654 with mode 0400 (`docs/operations/runbooks/secrets.md`).
  - Two external volumes outlive every `docker compose down -v`: `erp-ai-pro-data-protection-keys` (ADR-0032) and `erp-ai-pro-proxy-data`. The second holds the proxy's root key, which every trusting device accepts for any name (residual risk 16).
  - The migrator's `retain` command deletes what is past its retention without migrating (ADR-0043), and nothing ran it between deployments.
- **The owner's server, measured on 2026-10-18.**
  - A bare-metal Ubuntu 26.04 LTS with its clock in UTC, Docker from Docker's apt repository, sign-in through the socket-activated `ssh.socket`, and `sudo-rs` as its sudo.
  - Its IPv4 address on the home network comes from the router's DHCP, and SLAAC gives it a global IPv6 address from the internet provider's prefix.
  - As Ubuntu leaves a new server: ufw installed but off, password sign-in allowed (with an installer's `50-cloud-init.conf` in `sshd_config.d`), and the first account a member of `docker` and of `lxd`, whose `lxd-installer.socket` installs LXD on demand for that group.
- **Owner decisions of 2026-10-18.**
  - The host restarts itself at 03:00 India time when an update needs it.
  - SSH accepts keys only, from the home network only.
  - The lockout tool fail2ban, which the roadmap scope named, is declined.
- **What the tools document.**
  - Docker publishes a port without an address "to all host addresses (`0.0.0.0` and `[::]`)". On the server's Docker 29.9.1, `-p 0.0.0.0:18080:80` bound IPv4 only, and `-p 18081:80` bound both (measured).
  - "When you expose container ports using Docker, these ports bypass your firewall rules" (Docker's Ubuntu install guide), and "the docker group grants root-level privileges to the user" (Linux post-installation steps).
  - Live restore keeps containers running while the daemon restarts, across patch releases only. `live-restore` is applied by a configuration reload, and `log-opts` only by a restart (`dockerd` reference, "Configuration reload behavior").
  - A container under `restart: unless-stopped` that was stopped "manually or otherwise" is not restarted when the daemon starts (Docker, "Start containers automatically").
  - For each keyword "the first obtained value will be used", and `Include` expands its glob "in lexical order" (`sshd_config(5)`). `sshd -T` applies `Match` blocks only for the connection given with `-C` (`sshd(8)`).
  - Unattended upgrades ship with the official Ubuntu origins only. `Automatic-Reboot` defaults to false, and an `hh:mm` `Automatic-Reboot-Time` is "in the local system's timezone". needrestart restarts affected services by itself since Ubuntu 24.04, and Ubuntu's `needrestart.conf` excludes `docker` (Ubuntu Server, "Automatic updates", read on the host).
  - A unit in `Requires=` that is explicitly stopped or restarted stops or restarts its dependents, while `Wants=` does not. A failed `ConditionPathExists=` skips a unit without failing it (`systemd.unit(5)`). A oneshot service may use `Restart=on-failure`, which also covers a start that times out (`systemd.service(5)`). A calendar event may name an IANA time zone (`systemd.time(7)`).
  - sudo resets the environment, and sudo-rs runs the command "in a pseudo-terminal" of its own, its `use_pty` being "on by default" (sudo-rs `sudoers(5)`). On the server `who` lists no session at all, so `who -m` cannot name the one that runs the bootstrap. The shells above it keep `SSH_CONNECTION`, which sshd sets (measured).
  - "The root user and all members of the `lxd` group can interact with the local daemon", and that access "always grants full access to LXD" (LXD documentation, "Security").

## Decision

- **`scripts/deploy/bootstrap.sh`.**
  - The administrator runs it once with sudo from their own account, from a checkout of this repository: `--site-address <the fixed IPv4 address>`, `--authorized-keys <file>`, and optionally `--home-network <CIDR>`, which defaults to the site address's network.
  - Every step compares, then changes only what differs, and a second run prints `nothing to change: the host already matches`.
  - `--dry-run` changes nothing. Without root it compares what it can read and lists the rest as changes for a run with sudo to compare.
  - It refuses:
    - Ubuntu other than 22.04, 24.04 or 26.04 LTS, and a clock other than UTC;
    - a site address that is not the host's;
    - a keys file with a line that is not a public key.
- **The deploy account `erp-ai-pro`.**
  - Home `/srv/erp-ai-pro` (0750); no usable password (`*`); its `authorized_keys` is the given file.
  - That file must hold keys of its own: the bootstrap refuses one that also signs in as the administrator, because the account is root through Docker.
  - It is the only member of `docker`: the bootstrap removes every other member, the administrator included, who from then on runs Docker through sudo. It refuses a host where `docker` is someone's primary group.
- **No root through LXD.** The bootstrap removes every member of `lxd` and masks `lxd-installer.socket`, so LXD, preinstalled as a snap on Ubuntu 22.04 and installed on demand from 24.04, answers root alone.
- **The deployment checkout.**
  - `/srv/erp-ai-pro/ERP-AI-Pro` is cloned from the checkout the bootstrap runs from, at its commit, with that checkout's `origin`, into a temporary folder. That folder takes the checkout's place once it is complete and owned by the deploy account. A checkout found with another owner is given back to the deploy account. Otherwise deployments move it, and the bootstrap never does.
  - It holds `infra/compose/secrets` (0700, the deploy account's) for the two client certificates. The administrator installs them with sudo, owned by 1654 with mode 0400.
  - `infra/compose/.env` (0600) is copied once from `.env.example` with `ERP_SITE_ADDRESS` filled in. A later run only warns when the address differs, because the address changes together with the production sign-in settings (ADR-0048).
  - The bootstrap creates both external volumes when they are missing.
- **Docker.**
  - Docker Engine is installed from Docker's repository as its Ubuntu guide describes, unless a Docker Engine with the Compose plugin is already there. The guide's unofficial packages are refused. Docker's apt repository is added back when Docker's packages are there without it.
  - `/etc/docker/daemon.json` (`infra/host/docker/daemon.json`) turns on live restore and rotates the logs of containers that set no logging of their own (20 MB, 5 files).
    - An existing file passes when it holds those settings among its own, and is refused otherwise.
    - The settings are applied whenever the bootstrap writes the file or Docker reports live restore off. That means a reload, then waiting until Docker reports live restore on, then the restart for the log options, which keeps running containers.
  - Docker's packages come from Docker's repository, which unattended upgrades does not read, so they are updated by hand (`docs/operations/runbooks/host-setup.md`).
- **The ERP's ports are IPv4 only.** `compose.production.yaml` publishes the proxy's 80 and 443 on `0.0.0.0`. The router's address translation keeps the IPv4 ports inside the home network, since nothing is forwarded, and the global IPv6 address reaches no published port.
- **SSH.**
  - `infra/host/ssh/10-erp-ai-pro.conf` sorts before every other file of `sshd_config.d`. It sets `PasswordAuthentication no`, `KbdInteractiveAuthentication no`, `AuthenticationMethods publickey`, `PermitRootLogin no` and `X11Forwarding no`.
  - First, the bootstrap checks that the administrator has a usable key in `authorized_keys`, and that sshd's StrictModes would accept it: the home, `~/.ssh` and the file belong to the administrator or root, and nobody else may write them. It refuses `Match` blocks, whose settings it cannot prove.
  - Every run tests the configuration with `sshd -t`, putting back any rules it wrote when that fails, then reloads `ssh.service`. It proves the effective values with `sshd -T`, and refuses an sshd that listens on another port than 22.
- **The firewall.**
  - ufw covers IPv6 (`IPV6=yes`), denies new incoming connections by default, allows outgoing ones, and holds one rule: SSH over IPv4 from the home network. The bootstrap refuses any other rule and proves that `iptables` and `ip6tables` both drop by default.
  - Before turning the firewall on, it reads its own session's address from the `SSH_CONNECTION` of the processes that started it. It refuses an address outside the home network or over IPv6, and a session through sshd whose address it cannot read.
- **Nightly updates.**
  - `infra/host/apt/52erp-ai-pro-unattended-upgrades.conf` sets `APT::Periodic::Enable "1"`, keeps the daily list update and upgrade on, and sets `Automatic-Reboot "true"` and `Automatic-Reboot-Time "21:30"`, which is 03:00 India time on a UTC clock.
  - The bootstrap unmasks the update services and timers, enables the timers, and proves the settings with `apt-config dump`.
  - It retries `apt-get update` while the daily update holds the package lists.
- **systemd** (`infra/host/systemd`).
  - `erp-ai-pro.service` (oneshot, `RemainAfterExit`, `User=erp-ai-pro`) brings the last deployment's containers back at boot: `docker compose … up --detach --wait --no-build --no-recreate --pull never --no-deps api web proxy`.
    - It never runs the migrator, never replaces a container and never pulls.
    - `ExecStop` stops the stack in order at shutdown, so after a clean shutdown this unit is what starts it again, and `Restart=on-failure` tries every minute until a start succeeds.
    - It `Wants=` docker.service, so a Docker update's restart leaves the stack to live restore.
  - `erp-ai-pro-retain.timer` runs `erp-ai-pro-retain.service`, `docker compose … run --rm -T migrator retain`, at 01:00 India time with up to 15 minutes' delay. It is `Persistent`, so a run the host slept through happens at the next boot, and the journal keeps the output and exit code of each run.
  - Both services wait, skipped rather than failed, for `.env` and their client certificates.
- **Verification.**
  - `scripts/deploy/check.sh` runs in `verify.ts` and CI.
    - It syntax-checks and shellchecks the deploy scripts.
    - It checks that the units name the deploy account, the checkout and only the stack's files, that the boot unit never migrates, recreates or pulls and retries a failed start, that the nightly unit runs `retain` alone, and that the daemon configuration is JSON.
    - Where the tools exist, it runs `systemd-analyze verify`, `apt-config` and `dockerd --validate`.
  - The `docker-build` jobs `Bootstrap a fresh Ubuntu host` (`scripts/deploy/tests/bootstrap-runner.sh`) run on GitHub-hosted Ubuntu 22.04, 24.04 and 26.04 runners. Each takes Docker off the runner and masks the update services, then proves:
    - a deploy key shared with the administrator is refused;
    - a dry run without root and one with sudo change nothing;
    - of two runs with sudo, the second changes nothing, nor does a dry run after it;
    - the deploy account signs in with its key and reaches Docker, installed from Docker's repository with live restore and rotated logs;
    - a password sign-in is refused;
    - `sshd -T`, the firewall's one rule and its IPv4 and IPv6 policies, the `docker` and `lxd` groups, the units, the unmasked update services, the APT settings, the checkout with its secrets folder and `.env`, and the volumes are as decided;
    - the production configuration publishes 80 and 443 on `0.0.0.0` only.

## Options rejected

- **fail2ban.** With passwords off, there is nothing to guess. With SSH admitted from the home network only, no one outside can try. The package comes from Ubuntu's community-maintained `universe`, and a ban by address could lock out the administrator's own computer after a mistake. The owner declined it.
- **The administrator in `docker` or `lxd`, or sharing their keys with the deploy account.** Anyone who signs in as the administrator, with any key on that account, would be root without the password. They could read the proxy's root key, the client certificates and the key ring.
- **Docker's packages in unattended upgrades.** Docker's repository has no security pocket, so every release, minor and major alike, would install unattended in the middle of the day, and live restore covers patch releases only.
- **A boot unit that runs `docker compose up --wait` alone.** It would run the migrator's seeders and sweeps at every boot. After a deployment interrupted between pulling and migrating, it would start a new api against a schema without its migrations.
- **`Requires=docker.service`.** Restarting Docker for an update would then stop and restart the whole stack.
- **Rules in Docker's `DOCKER-USER` chain** to keep 80 and 443 to the home network. They would live outside ufw and need persisting of their own, while the router's address translation already keeps the IPv4 ports in, and IPv6 publishes none.
- **A static address set on the host.** The router's DHCP could hand the same address to another device. A reservation in the router keeps one owner of the address.
- **Rootless Docker.** It changes where volumes live and how ports below 1024 are bound. The stack already runs every container non-root with every capability dropped.

## Consequences

- **The administrator.** They run Docker through sudo, or as `erp-ai-pro` with its own key. Signing in over the network needs a key; the console still takes the password.
- **The deploy key.** It is a root key, kept only where deployments run.
- **The nightly restart.** On a night when an update needs a restart, the ERP is off for a few minutes after 03:00 India time. `erp-ai-pro.service` then starts the containers that its `ExecStop` stopped. The restart policy brings them back only after a crash or a power loss.
- **Retention.** Once the certificates are in place, security events and change records past their retention are deleted every night. That narrows residual risk 14 to a timer that stops or a run that fails, whose result the journal keeps.
- **The checkout's files.** It holds `.env` and the certificates, which git ignores. In the checkout, `git clean -x` deletes `.env`, and with `-d` the certificates too. Deployments (`first-deployment-release-and-deploy-pipeline`) sign in as `erp-ai-pro` and move the checkout to a release.
- **The units start working at go-live.** They do nothing until the client certificates are installed (`first-deployment-production-database-provisioning`, `first-deployment-go-live-checklist-and-production-smoke`).
- **The router.** It must reserve the address for the host's network adapter and forward no port.
