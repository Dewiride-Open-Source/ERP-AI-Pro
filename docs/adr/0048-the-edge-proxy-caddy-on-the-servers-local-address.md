# ADR-0048: The edge proxy: Caddy on the server's local address

Status: accepted
Date: 2026-10-18

## Context

- **What existed.**
  - `infra/compose/compose.yaml` ran the migrator, the api and the web containers, and the production overlay hardened them; nothing published a port in production.
  - The edge proxy owns TLS, the HTTPS redirect and HSTS (ADR-0019); the API trusts forwarded headers only from the compose network (`Erp:Platform:Host:KnownNetworks`, ADR-0018) and answers only its allowed host names (`Erp:Platform:Host:AllowedHosts`).
  - Sign-in builds `redirect_uri` from `Erp:Platform:Identity:WebOrigin` (ADR-0031), and the session and antiforgery cookies are `__Host-` cookies with `Secure` (ADR-0031, ADR-0033), so a browser keeps a session only over HTTPS, `http://localhost` aside.
- **Owner decisions of 2026-10-18.**
  - The nine items of the first deployment are Community. The owner's own server goes live on the Community images and switches to the Enterprise images once `business-building-blocks-enterprise-edition-composition` builds them.
  - The front door is Caddy (Apache-2.0; its repository: "Caddy is a project of ZeroSSL, an HID Global company"). Also offered:
    - nginx with certbot (BSD-2-Clause, Nginx, Inc.; Apache-2.0, the Electronic Frontier Foundation);
    - Microsoft YARP (MIT), whose Let's Encrypt add-on Microsoft Learn calls archived and not recommended (`LettuceEncrypt`).
  - No domain for now: the home connection's public address changes, so people open the ERP at the server's local address (`192.168.x.x`), from inside the home network only.
- **Caddy** (caddyserver.com: "Automatic HTTPS", "reverse_proxy", "Global options", "Running"; Docker Hub `caddy`).
  - "Caddy serves IP addresses and local/internal hostnames over HTTPS using self-signed certificates": it creates a certificate authority of its own for them, because a publicly-trusted certificate needs a name that "is not an IP address". For a public name it obtains one from an ACME authority such as Let's Encrypt.
  - What 2.11.7 issued when measured on 2026-10-18:
    - the root `/data/caddy/pki/authorities/local/root.crt`, valid for 3,600 days;
    - an intermediate valid for 7 days (the documented default);
    - a leaf certificate valid for 12 hours.
    Caddy renews the last two itself. The root keeps its fingerprint across restarts and across new containers on the same volume, and "it's very important to persist the data directory".
  - "By default, Caddy passes through incoming headers—including `Host`—to the backend without modifications". It also:
    - sets `X-Forwarded-For`, `X-Forwarded-Proto` and `X-Forwarded-Host`, and ignores those headers from clients unless they come from `trusted_proxies`, of which there are none;
    - streams request bodies, and has no default read, write or response-header timeout towards an upstream;
    - limits a server only by `read_header` (1 minute) and `idle` (5 minutes).
  - The official image runs as root, and `/usr/bin/caddy` carries the file capability `cap_net_bind_service=ep`. With every capability dropped it does not start: "exec /usr/bin/caddy: operation not permitted" (measured), because the kernel refuses a file whose forced capabilities lie outside the bounding set. A copy without the capability, as a non-root user with every capability dropped and `net.ipv4.ip_unprivileged_port_start` 0, binds port 443 (measured).
  - Docker's documentation states no default for `net.ipv4.ip_unprivileged_port_start`, though the engine measured here sets 0. A container may set the namespaced `net.*` sysctls (`docker run --sysctl`).
- **Standards and Entra.**
  - A browser that opens an IP address sends no server name: "Literal IPv4 and IPv6 addresses are not permitted in "HostName"" (RFC 6066, section 3).
  - A browser ignores HSTS for such an address: when the host "syntactically matches the IP-literal or IPv4address productions", "the UA MUST NOT note this host as a Known HSTS Host" (RFC 6797, section 8.1.1).
  - Microsoft Entra requires the `https` scheme for a redirect URI that is not localhost (Microsoft Learn, "Redirect URI (reply URL) outline and restrictions", whose own example is `https://127.0.0.1/auth-response`).

## Decision

- **The proxy image.** `ghcr.io/dewiride-open-source/erp-ai-pro/proxy` is built here by `infra/docker/proxy.Dockerfile`, from `caddy:2.11.7-alpine` pinned by digest:
  - the file capability is removed (`setcap -r /usr/bin/caddy`);
  - the user `caddy` (10001:10001) owns `/data` and `/config` with mode 0700;
  - `infra/proxy/Caddyfile` is copied in;
  - the health check reads the admin endpoint, which listens on `127.0.0.1:2019` inside the container only.
- **The Caddyfile.** One site, `{$ERP_SITE_ADDRESS}`:
  - `/api/*` goes to `api:8080` and everything else to `web:3000`, so uploads, the OpenID Connect callbacks and the change history's export never pass through the web container;
  - Caddy's automatic HTTPS redirects HTTP with a 308, and the site speaks HTTP/1.1 and HTTP/2;
  - `default_sni` is the site address, `persist_config off` and `skip_install_trust` are set.
  An IP address gets a certificate from Caddy's own authority, and a public name one from Let's Encrypt. Moving to a domain later changes the address and the settings below, never the image.
- **The compose service `proxy`.** It waits for a healthy api and web, sets `net.ipv4.ip_unprivileged_port_start: 0` itself rather than relying on the engine's default, and runs on the compose network.
  - In production (`compose.production.yaml`):
    - its 80 and 443 are the only published ports;
    - `read_only` with a tmpfs `/tmp`, `cap_drop: [ALL]` with nothing added back, and `no-new-privileges`;
    - 0.5 CPU and 256 MiB, and rotated logs;
    - `/data` in the external volume `erp-ai-pro-proxy-data`, created once with `docker volume create` and never removed, so `docker compose down -v` keeps the authority every device trusts;
    - `ERP_SITE_ADDRESS` is required, and a stack without it does not start.
  - The local stack (`compose.override.yaml`) serves `127.0.0.1` on `127.0.0.1:8080` and `127.0.0.1:8443`, with no volume.
- **One home for the address.** `Erp:Platform:Identity:WebOrigin` under the production label (`https://<address>`) holds it; `scripts/azure/entra.sh` writes it from `ERP_AZURE_PRODUCTION_WEB_ORIGIN` while registering the production redirect URIs, and its origin check accepts an IP address. `ERP_SITE_ADDRESS` in the server's `.env` is its host, and the production `Erp:Platform:Host:AllowedHosts` is `<address>;api;127.0.0.1`.
- **Devices trust the root.** Every computer and phone that opens the ERP trusts the proxy's root certificate once (`docs/operations/runbooks/proxy.md`). Without it the browser refuses the connection, and with it the session cookie.
- **Verification.** The compose smoke test (`scripts/checks/compose-smoke.ts`):
  - checks that the image runs as `caddy` and holds `/data` owned by 10001:10001 with mode 0700;
  - follows the HTTP redirect;
  - reaches the sign-in page and the API over HTTPS at `127.0.0.1:8443`, without a server name and trusting only the proxy's own root, as a browser on the home network does.
  `docker-build.yml` and `verify.ts --docker` build the image and validate both configurations.

## Options rejected

- **The official image as root with `NET_BIND_SERVICE` added back.** The container that faces the network would run as root.
- **Unprivileged ports inside the container, published as 80 and 443.** Caddy's automatic redirect would then point at the inner HTTPS port. The redirect would need a site of its own and the ACME challenges a second mapping, for nothing the sysctl does not already give.
- **nginx with certbot, or YARP.** The owner chose Caddy (Context).
- **A public name that points at the local address**, with a certificate through the DNS challenge. It needs a Caddy build with the DNS provider's plugin and credentials for the domain's DNS, and the owner chose no domain for now. It remains the way to end the per-device trust.
- **HTTP/3.** UDP port 443 is not published, so browsers would be offered a protocol they cannot reach.

## Consequences

- **The root key can sign for any name.** Its private key in the volume can sign a certificate for any name, and every device that trusts the root would accept it. Docker keeps the volume under its own root-only directory, so reading it takes root or membership of the `docker` group on the server. The deploy user must be the only such member (host bootstrap), and backups of the volume must be encrypted (backups item). Residual risk 16 of `docs/security/auth-threat-model.md`.
- **Removing the volume replaces the root.** Caddy then creates a new root, and every device must trust it again.
- **HSTS does nothing yet.** It has no effect for an IP address, so the HSTS ramp-up of `first-deployment-reverse-proxy-and-tls` applies once the ERP has a public name.
- **The proxy has a check of its own.** The .NET and web steps of `verify.ts` never touch it; `--docker` and `docker-build.yml` build it and run the smoke test through it. Dependabot's docker ecosystem for `/infra/docker` watches its base image.
- **The local stack's proxy changes root.** It gets a new root whenever its container is recreated; the local stack cannot sign anyone in anyway.
- **What stays with `first-deployment-reverse-proxy-and-tls`.** Its TLS policy, headers and limits, and the end-to-end check through the proxy with the Entra redirect flow.
