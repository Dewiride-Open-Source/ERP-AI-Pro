# Runbook: setting up the production host

The production stack runs on an Ubuntu LTS server in the office, reached at its fixed address on the home network. One script prepares that server and keeps it prepared: `scripts/deploy/bootstrap.sh`. Design: [ADR-0049](../../adr/0049-the-production-host-a-deploy-account-keys-from-the-home-network-and-nightly-updates.md); the proxy and its root certificate: [proxy runbook](proxy.md); deployments: [deploy runbook](deploy.md).

Two accounts work on the host:

- **The administrator** (the account made when Ubuntu was installed) runs the bootstrap with sudo. Afterwards it reaches Docker only through sudo.
- **`erp-ai-pro`**, the deploy account, owns the deployment checkout `/srv/erp-ai-pro/ERP-AI-Pro` and is the only member of the `docker` group. It has no password and signs in with a key of its own. Every `docker compose` command of the runbooks runs as this account, from `/srv/erp-ai-pro/ERP-AI-Pro/infra/compose` (or from the checkout's root with `-f infra/compose/...`): `ssh -i ~/.ssh/erp-ai-pro-deploy erp-ai-pro@<address>`, or `sudo -iu erp-ai-pro` from the administrator's session.

## Before the bootstrap

1. **Ubuntu.** Ubuntu Server 22.04, 24.04 or 26.04 LTS (64-bit), with "Install OpenSSH server" ticked, on a wired connection. Keep the clock in UTC, the installer's default: the nightly restart time assumes it, and the bootstrap refuses another time zone. Keep SSH on port 22, and keep `Match` blocks out of the SSH configuration; the bootstrap refuses both.
2. **A fixed address.** In the router, reserve an address for the server's network adapter (a DHCP reservation, also called a static lease). `ip -brief link` on the server shows the adapter's MAC address, and `ip -4 -brief address` the address it holds. Forward no port to the server and put it in no DMZ: the ERP is reached from the home network only (owner decision of 2026-10-18).
3. **The administrator's key.** From the administrator's computer, copy its public key to the server and sign in with it once:

   ```bash
   ssh-copy-id -i ~/.ssh/<key>.pub <administrator>@<address>
   ssh -i ~/.ssh/<key> <administrator>@<address>
   ```

   The bootstrap turns passwords off over the network, and refuses to while the administrator has no usable key in `~/.ssh/authorized_keys`, or while sshd would refuse it because others may write the home folder, `~/.ssh` or that file. The server's own keyboard and screen keep accepting the password.
4. **The deploy account's own key.** On the computer that runs deployments, make a key pair for `erp-ai-pro` alone and copy its public half to the administrator's home on the server:

   ```bash
   ssh-keygen -t ed25519 -f ~/.ssh/erp-ai-pro-deploy -C erp-ai-pro-deploy
   scp ~/.ssh/erp-ai-pro-deploy.pub <administrator>@<address>:~/erp-ai-pro-deploy.pub
   ```

   The deploy account is root through Docker, so its key is a root key. Keep the private half only where deployments run. The bootstrap refuses a key that also signs in as the administrator.

## Running the bootstrap

Sign in as the administrator over SSH, through the server's IPv4 address from a device on the home network, and clone the release to deploy (the default branch, or `--branch <tag>`):

```bash
git clone https://github.com/Dewiride-Open-Source/ERP-AI-Pro.git
```

See what it would change. The dry run needs no root and changes nothing:

```bash
bash ERP-AI-Pro/scripts/deploy/bootstrap.sh --dry-run --site-address <address> --authorized-keys ~/erp-ai-pro-deploy.pub
```

Then run it with sudo:

```bash
sudo bash ERP-AI-Pro/scripts/deploy/bootstrap.sh --site-address <address> --authorized-keys ~/erp-ai-pro-deploy.pub
```

| Option | Meaning |
|---|---|
| `--site-address <IPv4>` | The server's fixed address: the address people open the ERP at, written to `.env` as `ERP_SITE_ADDRESS`. It must be one of the server's own addresses. |
| `--authorized-keys <file>` | The deploy account's own public keys, in `authorized_keys` format. None may also sign in as the administrator. |
| `--home-network <CIDR>` | The network SSH is admitted from. The default is the network of the site address, such as `192.168.1.0/24`. |
| `--backup-recipient <file>` | The public certificate the nightly backup seals its archives to, from `export-client-certificate.sh backup-recipient` ("The backup's certificates" below). The run refuses a file that holds a private key or an RSA key of fewer than 4096 bits. Without the option, a certificate already placed stays as it is. |
| `--dry-run` | List what a run would change and change nothing. Without root it lists what it cannot read as changes for a run with sudo to compare. |

Each step changes only what differs, and the run ends with the list of what it changed. A second run prints `nothing to change: the host already matches`.

| Area | What the bootstrap leaves |
|---|---|
| Packages | `openssh-server`, `ufw`, `unattended-upgrades`, `git`, `ca-certificates`, `curl`, `python3`, `gpg`, `openssl` (which seals the backup archives) |
| The GitHub CLI | `gh` 2.102.0 or later at `/usr/bin/gh`, which `deploy.sh` verifies the release images with, from GitHub's apt repository: the source `/etc/apt/sources.list.d/github-cli.sources`, and the keyring `/etc/apt/keyrings/githubcli-archive-keyring.gpg` (0644), trusted only when every key in it is one GitHub publishes and its signing key `7F38BBB59D064DBCB3D84D725612B36462313325` is there. Any other apt source naming `https://cli.github.com/packages` stops the run |
| Docker Engine | Docker's own packages from Docker's apt repository (`docker-ce`, `docker-ce-cli`, `containerd.io`, `docker-buildx-plugin`, `docker-compose-plugin`), installed only when no Docker Engine with Compose is there; Docker's repository added back when its packages are there without it; the unofficial packages Docker asks to remove first are refused (`docker.io`, `docker-compose`, `docker-compose-v2`, `docker-doc`, `docker-buildx`, `podman-docker`, `containerd`, `runc`) |
| `/etc/docker/daemon.json` | live restore, and logs rotated at 20 MB × 5 for containers without logging of their own (`infra/host/docker/daemon.json`); an existing file must hold those settings among its own, or the run stops and names them |
| The deploy account | `erp-ai-pro`: home `/srv/erp-ai-pro` (0750), no password, its own keys from `--authorized-keys`, the only member of `docker`. Every other member is removed, the administrator included |
| LXD | nobody in the `lxd` group, the administrator included, and `lxd-installer.socket` masked, because LXD gives its group root; LXD itself, preinstalled on 22.04, then answers root alone |
| The deployment checkout | `/srv/erp-ai-pro/ERP-AI-Pro` at the commit the bootstrap ran from, with `infra/compose/secrets` (0700), `infra/compose/.env` (0600, from `.env.example`, `ERP_SITE_ADDRESS` filled in) and, with `--backup-recipient`, `infra/compose/secrets/erp-backup-recipient.crt` (root's, 0644); deployments move it afterwards |
| Docker volumes | `erp-ai-pro-data-protection-keys` and `erp-ai-pro-proxy-data`, never removed |
| systemd | `erp-ai-pro.service` (enabled), and `erp-ai-pro-retain.timer`, `erp-ai-pro-heal.timer` and `erp-ai-pro-backup.timer` (enabled, running) with their services (below) |
| Security updates | `/etc/apt/apt.conf.d/52erp-ai-pro-unattended-upgrades.conf`: daily updates, the GitHub CLI's repository included, and a restart at 21:30 UTC (03:00 India time) when one needs it; the update services and timers unmasked and enabled |
| SSH | `/etc/ssh/sshd_config.d/10-erp-ai-pro.conf`: public keys only, no root sign-in, no X11 forwarding, proven with `sshd -T` after every run reloads sshd |
| Firewall | ufw on IPv4 and IPv6, with one rule: SSH over IPv4 from the home network. Any other rule stops the run. The ERP's 80 and 443 are not ufw's: Docker admits them itself, on IPv4 only, and the router, with no port forwarded, keeps them inside the home network |

What keeps the run from locking anyone out:

- It refuses to turn passwords off while the administrator has no key that sshd would accept.
- It tests the SSH configuration with `sshd -t` and puts back any rules it wrote when that fails.
- It refuses an sshd that listens elsewhere than port 22, whether through `Port`, a `ListenAddress` or the socket that starts it, and `Match` blocks or other included files, whose settings it cannot prove.
- Before it turns the firewall on, it reads the address its own SSH session comes from. It refuses one outside the home network, an IPv6 address, a session that reached another port than 22, and a session through SSH whose address it cannot read.
- It admits SSH from the home network before it changes the firewall in any other way, and stops at any other rule before changing defaults, so removing that rule as it asks never closes SSH.

Afterwards:

- Sign in again before using Docker as the administrator, whose sessions keep the `docker` group until they end. From then on, use `sudo docker …` or the deploy account.
- Delete the administrator's clone and the copied public key (`rm -rf ~/ERP-AI-Pro ~/erp-ai-pro-deploy.pub`): the deployment checkout is the one that runs.

## Checking the result

From the computer that runs deployments, then from the administrator's computer:

```bash
ssh -i ~/.ssh/erp-ai-pro-deploy erp-ai-pro@<address> "docker info --format '{{.LiveRestoreEnabled}}'"
ssh -o PubkeyAuthentication=no <administrator>@<address>
```

The first prints `true`. The second fails with `Permission denied (publickey).`, because the server offers public keys only. On the server:

- `sudo ufw status verbose` shows one rule, `22/tcp ALLOW IN <home network>`;
- `getent group docker lxd` names `erp-ai-pro` in `docker` and nobody in `lxd`;
- a dry run with sudo from a fresh clone of the same commit prints `nothing to change: the host already matches`.

## The client certificates

The api and the migrator sign in to Azure with their own service principals, whose certificates (PEM, private key included) are compose file secrets in `/srv/erp-ai-pro/ERP-AI-Pro/infra/compose/secrets`:

- `erp-runtime-client.pem` comes from `bash scripts/azure/export-client-certificate.sh runtime`;
- `erp-migrator-client.pem` comes from `bash scripts/azure/export-client-certificate.sh migrator`.

Install both just before the first deployment ([deploy runbook](deploy.md), "The first deployment"): from then on `erp-ai-pro.service` starts the stack at boot and `erp-ai-pro-retain.timer` runs the migrator every night, and both fail until a deployment has written the release into `.env`.

Copy each to the administrator's home over SSH only, then install it for the image user 1654, the user of the chiseled aspnet image that both containers run as (shown for the runtime's; the migrator's is the same with `erp-migrator-client.pem`):

```bash
scp scripts/azure/out/erp-runtime-client.pem <administrator>@<address>:~/erp-runtime-client.pem
```

```bash
sudo install -o 1654 -g 1654 -m 0400 ~/erp-runtime-client.pem /srv/erp-ai-pro/ERP-AI-Pro/infra/compose/secrets/erp-runtime-client.pem
shred -u ~/erp-runtime-client.pem
```

Shred the workstation's copy as well (`shred -u scripts/azure/out/erp-runtime-client.pem`; `rm -P` on macOS). The deploy account cannot read the files: Docker mounts them into the containers. A rotation follows `secrets.md` section 5c.

## The backup's certificates

The nightly backup needs two more files in `/srv/erp-ai-pro/ERP-AI-Pro/infra/compose/secrets`, and `erp-ai-pro-backup.service` is skipped until both exist:

- **`erp-backup-client.pem`**, the certificate (PEM, private key included) of the backup service principal "ERP-AI-Pro Backup (production)", the one identity that may write to and read the backup container. `bash scripts/azure/export-client-certificate.sh backup` exports it; copy it to the administrator's home over SSH and install it like the other two, for 1654, the user the backup's Azure CLI container runs as:

  ```bash
  sudo install -o 1654 -g 1654 -m 0400 ~/erp-backup-client.pem /srv/erp-ai-pro/ERP-AI-Pro/infra/compose/secrets/erp-backup-client.pem
  shred -u ~/erp-backup-client.pem
  ```

- **`erp-backup-recipient.crt`**, the public certificate of the RSA 4096 key in `kv-erp-ai-pro-ops` that every archive is sealed to. `bash scripts/azure/export-client-certificate.sh backup-recipient` exports it and prints its SHA-256 fingerprint. It holds no key and is no secret. Copy it to the administrator's home and place it with the bootstrap, run again from a clone of the running release ("Upkeep" below), which refuses a file that holds a private key:

  ```bash
  sudo bash ERP-AI-Pro/scripts/deploy/bootstrap.sh --site-address <address> --authorized-keys /srv/erp-ai-pro/.ssh/authorized_keys --backup-recipient ~/erp-backup-recipient.crt
  rm ~/erp-backup-recipient.crt
  sudo openssl x509 -in /srv/erp-ai-pro/ERP-AI-Pro/infra/compose/secrets/erp-backup-recipient.crt -noout -fingerprint -sha256
  ```

  The fingerprint the last command prints is the one the export printed. It needs `sudo` because the folder is the deploy account's.

The private key that opens the archives never stays on the server: the operator exports it for a restore alone (`export-client-certificate.sh backup-recipient-key`), and `restore.sh` shreds it once the restore is done ([restore runbook](restore.md)). A rotation follows `secrets.md` sections 5c (the client certificate) and 5g (the recipient).

## The `.env` values

`/srv/erp-ai-pro/ERP-AI-Pro/infra/compose/.env` holds the stack's non-secret values. Edit it as `erp-ai-pro`. `.env.example` explains every key, and the keys that matter in production are these:

| Key | Value | Set by |
|---|---|---|
| `ERP_SITE_ADDRESS` | the server's fixed address | the bootstrap; it changes only together with the production sign-in settings ([proxy runbook](proxy.md), "The address") |
| `IMAGE_TAG`, `API_IMAGE_DIGEST`, `WEB_IMAGE_DIGEST`, `PROXY_IMAGE_DIGEST` | the release that runs: its version without the `v`, and the digest of each of its images, which `compose.production.yaml` runs them by | `deploy.sh` and `rollback.sh`, never by hand |
| `APPCONFIG_ENDPOINT` | `https://<store>.azconfig.io`, the one App Configuration store | the go-live |
| `AZURE_TENANT_ID` | the Entra tenant id, which the backup signs in to as well | the go-live |
| `AZURE_CLIENT_ID` | the runtime service principal's application id | the go-live |
| `MIGRATOR_AZURE_CLIENT_ID` | the migrator service principal's application id, which `entra.sh` prints as `MIGRATOR_AZURE_CLIENT_ID` | the go-live, together with `erp-migrator-client.pem` |
| `BACKUP_BLOB_ENDPOINT` | `https://<account>.blob.core.windows.net/`, the blob endpoint of the storage account kept for the backups alone, which `provision.sh` prints as `BACKUP_BLOB_ENDPOINT` | the go-live |
| `BACKUP_CONTAINER` | `host-backups`, the write-once container of that account, which `provision.sh` prints as `BACKUP_CONTAINER` | the go-live |
| `BACKUP_AZURE_CLIENT_ID` | the backup service principal's application id, which `entra.sh` prints | the go-live, together with `erp-backup-client.pem` |
| `COMPOSE_NETWORK_CIDR` | the compose network, `172.28.0.0/16` unless that range is taken on the home network | rarely |

The ids, the endpoint and the container name are not secrets. No secret ever goes into `.env`. Beside it, `.env.previous` (0600) holds the four release values of the release before, which `deploy.sh` keeps and `rollback.sh` returns to ([deploy runbook](deploy.md)). The nightly backup keeps both files; `backup.sh` and `restore.sh` read the backup values as data and refuse a value of another shape, a key set twice and a key set in another form than `KEY=value`.

## At boot, every minute and every night

- **`erp-ai-pro.service`** starts at boot, as `erp-ai-pro` in the compose folder. It runs `docker compose -f compose.yaml -f compose.production.yaml up --detach --wait --no-build --no-recreate --pull never --no-deps api web proxy` under `flock /srv/erp-ai-pro/deploy.lock`. That brings the last deployment's containers back as they are and waits until they are healthy. It never runs the migrator, never replaces a container and never pulls an image, because only a deployment does those ([deploy runbook](deploy.md)). At shutdown it stops the stack in order, so after a clean shutdown it is what starts the stack again: the containers' restart policy brings them back only after a crash or a power loss. A start that fails, such as one before the internet link returns, tries again every minute.
- **`erp-ai-pro-retain.timer`** runs `erp-ai-pro-retain.service` at 01:00 India time, up to 15 minutes later, or at the next boot when the server was off. The service runs `docker compose -f compose.yaml -f compose.production.yaml run --rm -T migrator retain`, the retention sweeps without a migration, under the same lock.
- **`erp-ai-pro-heal.timer`** runs `erp-ai-pro-heal.service` every minute. The service runs `scripts/deploy/heal.sh` of the deployment checkout, which restarts a running container that Docker reports unhealthy once it has run 30 minutes, so a part of the ERP that stops answering without exiting comes back by itself: the containers' restart policy acts only when one exits ([ADR-0055](../../adr/0055-the-production-host-restarts-an-unhealthy-container-and-alerts-nobody.md)). It never starts a stopped container. It takes the lock without waiting, so a minute in which a deployment, a rollback, a restore, the retention run, the backup or the start at boot holds it is skipped. Nothing tells anyone about an outage or a restart (owner decision of 2026-10-30): each restart, and each unhealthy container it leaves alone because it started less than 30 minutes ago, is a line of its journal.
- **`erp-ai-pro-backup.timer`** runs `erp-ai-pro-backup.service` at 02:00 India time, after the retention run and before the update restart, or at the next boot when the server was off. The service runs `scripts/deploy/backup.sh` of the deployment checkout, which copies the server's own files off it every night ([restore runbook](restore.md)):
  - it reads the volumes `erp-ai-pro-data-protection-keys` and `erp-ai-pro-proxy-data` through the BusyBox image of `infra/compose/compose.backup.yaml`, as each volume's owner (1654 and 10001), read-only, with no network, no capability and no container log, so the volumes' contents reach `backup.sh` alone and never Docker's log on disk, and adds `.env`, `.env.previous` and a manifest of their SHA-256 hashes, the releases they name, the proxy root's fingerprint and the key ring's keys with the Key Vault key versions that wrap them;
  - it keeps that plaintext copy in the service's runtime folder `/run/erp-ai-pro-backup`, in memory and private to `erp-ai-pro`, which systemd removes when the run ends;
  - it seals the archive with the host's OpenSSL (CMS, AES-256-GCM, its key wrapped with RSA-OAEP-SHA256) to `erp-backup-recipient.crt`, so only the private key in `kv-erp-ai-pro-ops` opens it;
  - through the Azure CLI image of the same file, signed in as the backup service principal, it uploads the archive into `BACKUP_CONTAINER` as `<yyyy>/<mm>/<yyyymmddThhmmssZ>.tar.p7m`, never over an existing blob, reads its length and MD5 back, and deletes the archives older than 90 days and a day, which the container's write-once policy keeps until then;
  - it holds the lock while it copies the files, waiting up to 14 minutes for it and failing that night's run when it does not get it, and lets it go before it seals and uploads the copy. Nothing tells anyone about a failed backup (owner decision of 2026-10-30): each step is a line of its journal, and a failure names the step it stopped at.

The lock is the one `deploy.sh`, `rollback.sh` and `restore.sh` hold while they run, so no unit starts in the middle of a deployment: the boot and retention units wait for the lock until their start times out (10 minutes for the stack, 30 for the retention run), and the backup waits up to 14 minutes of its 30. The heal service holds it for the seconds it takes to look at the stack, and the backup for the seconds it takes to copy the files. Stopping the stack takes no lock, so a shutdown never waits.

The boot and nightly services are skipped, not failed, until `.env` and their certificates exist: the client certificates for the boot and retention units, `erp-backup-client.pem` and `erp-backup-recipient.crt` for the backup ("The backup's certificates" above). The boot and retention units fail until the first deployment has written the release into `.env`, because `compose.production.yaml` refuses to resolve without its digests; the backup does not need a release, and before the first deployment it backs up volumes no container has filled yet. The heal service needs only `.env`, and before the first deployment it finds no container to look at. Their state and output stay in the journal:

```bash
systemctl status erp-ai-pro.service
systemctl list-timers erp-ai-pro-retain.timer erp-ai-pro-heal.timer erp-ai-pro-backup.timer
journalctl -u erp-ai-pro.service -u erp-ai-pro-retain.service -u erp-ai-pro-heal.service -u erp-ai-pro-backup.service --since today
docker compose -f compose.yaml -f compose.production.yaml ps
```

### Reading a backup's journal

```bash
journalctl -u erp-ai-pro-backup.service --since yesterday
```

A run prints one line per step, in this order: `settings` (the container, the service principal and the certificate the archives are sealed to, with its serial number and end date), `images`, `lock`, `volume erp-ai-pro-data-protection-keys`, `volume erp-ai-pro-proxy-data` (the entries and bytes read), `release files`, `unlock`, `manifest`, `seal`, `upload`, `check` and `prune` (a line for each archive it deletes, then the archives deleted and kept). A run that succeeded ends with `backed up the server's files to host-backups/<yyyy>/<mm>/<time>.tar.p7m; <n> archive(s) older than 90 days deleted`. A failed run ends with `[error] the backup stopped at the step '<step>': …`, which also says whether the night's archive reached the container and how many older archives it deleted before it stopped, and the unit shows as failed in `systemctl --failed`. A warning that the certificate the archives are sealed to expires within 30 days asks for its rotation (`secrets.md` section 5g); the archives keep opening with its key after that date.

To run a backup at once, as the administrator, for instance after placing a new certificate: `sudo systemctl start erp-ai-pro-backup.service`, which returns when the run ends.

### Restoring the server's files

`scripts/deploy/restore.sh` puts an archive back into the two volumes, `.env` and `.env.previous`, as `erp-ai-pro`, with the private key the operator exports for it. `--list` names every archive with the time the storage service created it, newest first, and needs no key; a dry run downloads, opens and checks one archive and lists what a restore would change. Both may run while the stack runs:

```bash
bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/restore.sh --list
bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/restore.sh --name <archive> --key <exported key> --dry-run
```

A restore itself refuses while a container of the stack runs (`sudo systemctl stop erp-ai-pro.service` first), and replaces volumes that hold files only with `--replace`. It gives each volume's top folder the owner and mode its image gives it, fills it as that owner and shreds the key file once it is done. It reads the container and the service principal from `.env`, so on a rebuilt server the bootstrap's fresh `.env` first takes `AZURE_TENANT_ID`, `BACKUP_BLOB_ENDPOINT`, `BACKUP_CONTAINER` and `BACKUP_AZURE_CLIENT_ID`, and `erp-backup-client.pem` is placed; the restored `.env` then takes its place. The [restore runbook](restore.md) holds the procedure, the rebuilt server and the rehearsal.

A failed `retain` exits 1 (a sweep failed, the others ran), 2 (its configuration is invalid) or 4 (the database holds migrations the release in `.env` does not know, so nothing was deleted; the journal shows `status=4/NOPERMISSION`, systemd's name for that code, not a permission problem), and the [migrations runbook](migrations.md) says what each means.

## Trusting the proxy's root on each device

People open `https://<address>`. The proxy serves it with a certificate from its own authority, so every computer and phone that opens the ERP trusts that authority's root once. Until it does, the browser refuses the page, and with it the session cookie, so sign-in fails.

1. Export the root and print its SHA-256 fingerprint on the server ([proxy runbook](proxy.md), "Exporting the root certificate"). `erp-proxy-root.crt` holds only the public certificate.
2. Copy `erp-proxy-root.crt` to the device, and before trusting it compare the SHA-256 fingerprint the device shows with the one the server printed. A different fingerprint means a different certificate: stop.
3. Trust it as a root certification authority:
   - **Windows** (Edge and Chrome use the Windows store). Open the file, choose **Install Certificate…**, choose **Local Machine** for everyone on the computer, then **Place all certificates in the following store** and **Trusted Root Certification Authorities**. Alternatively, from an elevated prompt:

     ```bat
     certutil -addstore Root erp-proxy-root.crt
     ```

     Use `certutil -user -addstore Root erp-proxy-root.crt` for the current user alone, without administrator rights.
   - **macOS** (Safari and Chrome use the keychains). Open Keychain Access, select the **System** keychain (or **login** for one user) and drag the file onto it, giving an administrator's name and password when asked. Then double-click the certificate, open **Trust**, and set **When using this certificate** to **Always Trust**.
   - **iPhone and iPad.** Open the file on the device, from Safari, Mail or AirDrop. iOS asks for permission to install it as a profile and shows what it contains; install it in Settings, which lists installed profiles under **General > VPN & Device Management**. Then turn trust on in **Settings > General > About > Certificate Trust Settings**, under "Enable full trust for root certificates".
   - **Android** (Chrome trusts user-installed CA certificates). Go to **Settings > Security & privacy > More security settings > Encryption & credentials > Install a certificate > CA certificate**, accept the warning "Your data won't be private" and pick the file. The menu names are Pixel's, and other phones name them similarly. Installed certificates are listed under **User credentials** and removed there.
   - **Firefox** 120 and later trusts, on Windows, macOS and Android, the roots added to the system, so the steps above cover it. If it still warns, check in its certificate settings, under Privacy & Security, that it imports the system's roots (`security.enterprise_roots.enabled` in `about:config`).
4. Open `https://<address>`: the page loads with no warning.

The root is valid for ten years and stays the same as long as the volume `erp-ai-pro-proxy-data` does. A device that no longer opens the ERP removes it. When the ERP gets a public name, every device removes it ([proxy runbook](proxy.md), "Moving to a public name").

## Upkeep

- **Ubuntu's updates.**
  - They install every day, and the server restarts at 03:00 India time when an update needs it. The ERP is then off for a few minutes and comes back through `erp-ai-pro.service`.
  - `cat /var/run/reboot-required.pkgs` names the packages that wait for a restart.
  - `/var/log/unattended-upgrades/` keeps what was installed.
  - After a restart night, `systemctl status erp-ai-pro.service` and `journalctl -u erp-ai-pro.service -b` show whether the stack came back.
- **Docker's updates.** Docker's packages come from Docker's repository, which the daily updates do not read. Check them once a month and whenever Docker announces a security fix. `apt list --upgradable` shows the versions.
  - A patch release (29.9.1 to 29.9.2, say) installs with the stack running, which live restore keeps up:

    ```bash
    sudo apt update && sudo apt install --only-upgrade docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    ```

    Then, as `erp-ai-pro` in the compose folder, `docker compose -f compose.yaml -f compose.production.yaml ps` must show every container running and healthy. The unit's status says nothing here: it stays as it was since the boot.
  - A minor or major release, for which live restore promises nothing, needs the stack stopped first: `sudo systemctl stop erp-ai-pro.service`, then the same upgrade, then `sudo systemctl start erp-ai-pro.service`. The start waits until every container is healthy, and fails otherwise.
- **Re-running the bootstrap.**
  - After changing the host by hand, or when `deploy.sh` warns that a release changes what the bootstrap installs (`infra/host`, `scripts/deploy/bootstrap.sh` or `scripts/deploy/lib/common.sh`), run it again from a fresh clone of the running release (`--branch <tag>`). It puts back what drifted and changes nothing else.
  - Pass the deploy account's present keys, which a run with sudo can read, so they stay as they are:

    ```bash
    sudo bash ERP-AI-Pro/scripts/deploy/bootstrap.sh --site-address <address> --authorized-keys /srv/erp-ai-pro/.ssh/authorized_keys
    ```

  - `--backup-recipient` may be left out: the backup's public certificate already placed stays as it is. Passing a new one replaces it, as a rotation does (`secrets.md` section 5g).
  - It never moves the deployment checkout or changes `.env` or the client certificates once they exist. It only gives files of the checkout that belong to someone else back to `erp-ai-pro`, the certificates aside.
- **The backup's helper images.** `infra/compose/compose.backup.yaml` pins the BusyBox and Azure CLI images by digest, and Dependabot proposes their updates like those of every other image. The first backup after a deployment that moves a pin pulls the new image, and the image it replaces stays on the server until it is removed: `docker image ls --digests mcr.microsoft.com/azure-cli` lists them, and `docker image rm mcr.microsoft.com/azure-cli@<digest>` removes one that `compose.backup.yaml` no longer names.
- **Never do these:**
  - remove the two volumes;
  - leave the backup's private key (`erp-backup-recipient-key.pem`) on the server after a restore, or place it as `erp-backup-recipient.crt`: whoever holds it opens every archive;
  - add an account to the `docker` or `lxd` group;
  - run `git clean` with `-x` in the deployment checkout: it deletes `.env` and `.env.previous`, and with `-d` the client certificates too;
  - edit the four release lines of `.env` or `.env.previous`, or move the deployment checkout by hand after the first deployment: `deploy.sh` and `rollback.sh` do both;
  - give `erp-ai-pro` a password, or the administrator's keys.
- **Losing every key** leaves the server's own keyboard and screen: sign in there with the administrator's password and add a key (`~/.ssh/authorized_keys`).
