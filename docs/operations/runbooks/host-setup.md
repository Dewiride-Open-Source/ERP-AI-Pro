# Runbook: setting up the production host

The production stack runs on an Ubuntu LTS server in the office, reached at its fixed address on the home network. One script prepares that server and keeps it prepared: `scripts/deploy/bootstrap.sh`. Design: [ADR-0049](../../adr/0049-the-production-host-a-deploy-account-keys-from-the-home-network-and-nightly-updates.md); the proxy and its root certificate: [proxy runbook](proxy.md); deployments: [migrations runbook](migrations.md).

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
| `--dry-run` | List what a run would change and change nothing. Without root it lists what it cannot read as changes for a run with sudo to compare. |

Each step changes only what differs, and the run ends with the list of what it changed. A second run prints `nothing to change: the host already matches`.

| Area | What the bootstrap leaves |
|---|---|
| Packages | `openssh-server`, `ufw`, `unattended-upgrades`, `git`, `ca-certificates`, `curl`, `python3` |
| Docker Engine | Docker's own packages from Docker's apt repository (`docker-ce`, `docker-ce-cli`, `containerd.io`, `docker-buildx-plugin`, `docker-compose-plugin`), installed only when no Docker Engine with Compose is there; Docker's repository added back when its packages are there without it; the unofficial packages Docker asks to remove first are refused (`docker.io`, `docker-compose`, `docker-compose-v2`, `docker-doc`, `docker-buildx`, `podman-docker`, `containerd`, `runc`) |
| `/etc/docker/daemon.json` | live restore, and logs rotated at 20 MB × 5 for containers without logging of their own (`infra/host/docker/daemon.json`); an existing file must hold those settings among its own, or the run stops and names them |
| The deploy account | `erp-ai-pro`: home `/srv/erp-ai-pro` (0750), no password, its own keys from `--authorized-keys`, the only member of `docker`. Every other member is removed, the administrator included |
| LXD | nobody in the `lxd` group, the administrator included, and `lxd-installer.socket` masked, because LXD gives its group root; LXD itself, preinstalled on 22.04, then answers root alone |
| The deployment checkout | `/srv/erp-ai-pro/ERP-AI-Pro` at the commit the bootstrap ran from, with `infra/compose/secrets` (0700) and `infra/compose/.env` (0600, from `.env.example`, `ERP_SITE_ADDRESS` filled in); deployments move it afterwards |
| Docker volumes | `erp-ai-pro-data-protection-keys` and `erp-ai-pro-proxy-data`, never removed |
| systemd | `erp-ai-pro.service` (enabled) and `erp-ai-pro-retain.timer` (enabled, running), with `erp-ai-pro-retain.service` (below) |
| Security updates | `/etc/apt/apt.conf.d/52erp-ai-pro-unattended-upgrades.conf`: daily updates, and a restart at 21:30 UTC (03:00 India time) when one needs it; the update services and timers unmasked and enabled |
| SSH | `/etc/ssh/sshd_config.d/10-erp-ai-pro.conf`: public keys only, no root sign-in, no X11 forwarding, proven with `sshd -T` after every run reloads sshd |
| Firewall | ufw on IPv4 and IPv6, with one rule: SSH over IPv4 from the home network. Any other rule stops the run. The ERP's 80 and 443 are not ufw's: Docker admits them itself, on IPv4 only, and the router, with no port forwarded, keeps them inside the home network |

What keeps the run from locking anyone out:

- It refuses to turn passwords off while the administrator has no key that sshd would accept.
- It tests the SSH configuration with `sshd -t` and puts back any rules it wrote when that fails.
- It refuses an sshd on another port than 22, and `Match` blocks, whose settings it cannot prove.
- Before it turns the firewall on, it reads the address its own SSH session comes from. It refuses one outside the home network, an IPv6 address, and a session through SSH whose address it cannot read.

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

- `erp-runtime-client.pem` comes from `bash scripts/azure/export-runtime-certificate.sh`;
- `erp-migrator-client.pem` comes with `first-deployment-production-database-provisioning`.

Copy each to the administrator's home over SSH only, then install it for the image user 1654, the user of the chiseled aspnet image that both containers run as:

```bash
scp scripts/azure/out/erp-runtime-client.pem <administrator>@<address>:~/erp-runtime-client.pem
```

```bash
sudo install -o 1654 -g 1654 -m 0400 ~/erp-runtime-client.pem /srv/erp-ai-pro/ERP-AI-Pro/infra/compose/secrets/erp-runtime-client.pem
shred -u ~/erp-runtime-client.pem
```

Shred the workstation's copy as well (`shred -u scripts/azure/out/erp-runtime-client.pem`; `rm -P` on macOS). The deploy account cannot read the files: Docker mounts them into the containers. A rotation follows `secrets.md` section 5c.

## The `.env` values

`/srv/erp-ai-pro/ERP-AI-Pro/infra/compose/.env` holds the stack's non-secret values. Edit it as `erp-ai-pro`. `.env.example` explains every key, and the keys that matter in production are these:

| Key | Value | Set by |
|---|---|---|
| `ERP_SITE_ADDRESS` | the server's fixed address | the bootstrap; it changes only together with the production sign-in settings ([proxy runbook](proxy.md), "The address") |
| `IMAGE_TAG` | the release to run | each deployment |
| `APPCONFIG_ENDPOINT` | `https://<store>.azconfig.io`, the one App Configuration store | the go-live |
| `AZURE_TENANT_ID` | the Entra tenant id | the go-live |
| `AZURE_CLIENT_ID` | the runtime service principal's application id | the go-live |
| `MIGRATOR_AZURE_CLIENT_ID` | the migrator service principal's application id | `first-deployment-production-database-provisioning` |
| `COMPOSE_NETWORK_CIDR` | the compose network, `172.28.0.0/16` unless that range is taken on the home network | rarely |

The ids are not secrets. No secret ever goes into `.env`.

## At boot and every night

- **`erp-ai-pro.service`** starts at boot, as `erp-ai-pro` in the compose folder. It runs `docker compose -f compose.yaml -f compose.production.yaml up --detach --wait --no-build --no-recreate --pull never --no-deps api web proxy`. That brings the last deployment's containers back as they are and waits until they are healthy. It never runs the migrator, never replaces a container and never pulls an image, because only a deployment does those ([migrations runbook](migrations.md)). At shutdown it stops the stack in order, so after a clean shutdown it is what starts the stack again: the containers' restart policy brings them back only after a crash or a power loss. A start that fails, such as one before the internet link returns, tries again every minute.
- **`erp-ai-pro-retain.timer`** runs `erp-ai-pro-retain.service` at 01:00 India time, up to 15 minutes later, or at the next boot when the server was off. The service runs `docker compose -f compose.yaml -f compose.production.yaml run --rm -T migrator retain`, the retention sweeps without a migration.

Both services are skipped, not failed, until `.env` and their client certificates exist. Their state and output stay in the journal:

```bash
systemctl status erp-ai-pro.service
systemctl list-timers erp-ai-pro-retain.timer
journalctl -u erp-ai-pro.service -u erp-ai-pro-retain.service --since today
```

A failed `retain` exits 1 (a sweep failed, the others ran) or 2 (its configuration is invalid), and the [migrations runbook](migrations.md) says what each means.

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
  - After changing the host by hand, or when a release changes `infra/host` or `scripts/deploy`, run it again from a fresh clone of the running release (`--branch <tag>`). It puts back what drifted and changes nothing else.
  - It never touches the deployment checkout, `.env` or the certificates once they exist.
- **Never do these:**
  - remove the two volumes;
  - add an account to the `docker` or `lxd` group;
  - run `git clean` with `-x` in the deployment checkout: it deletes `.env`, and with `-d` the client certificates too;
  - give `erp-ai-pro` a password, or the administrator's keys.
- **Losing every key** leaves the server's own keyboard and screen: sign in there with the administrator's password and add a key (`~/.ssh/authorized_keys`).
