# Runbook: the edge proxy and its root certificate

The proxy is the only container that publishes ports (80 and 443, on IPv4 only, so the server's global IPv6 address reaches none of them): it serves the ERP over HTTPS at `ERP_SITE_ADDRESS`, sends `/api/*` to the api container and everything else to the web container. Design: [ADR-0048](../../adr/0048-the-edge-proxy-caddy-on-the-servers-local-address.md), and for its TLS policy, limits, timeouts, headers and log [ADR-0052](../../adr/0052-the-edge-proxys-tls-policy-limits-timeouts-headers-and-access-log.md); the server itself: [host setup runbook](host-setup.md) ([ADR-0049](../../adr/0049-the-production-host-a-deploy-account-keys-from-the-home-network-and-nightly-updates.md)).

## The address

`ERP_SITE_ADDRESS` in `infra/compose/.env` on the host is the address people open the ERP at, without scheme or port: today the server's local address, such as `192.168.1.20`, which the router must keep fixed for the server. It is the host of `Erp:Platform:Identity:WebOrigin` under the production label, which `scripts/azure/entra.sh` writes from `ERP_AZURE_PRODUCTION_WEB_ORIGIN` (`https://<address>`) when it registers the production sign-in registration's redirect URIs, and the production `Erp:Platform:Host:AllowedHosts`, which the same run writes, starts with it (`<address>;api;127.0.0.1`), so the three always change together. The production stack refuses to start without it.

For an IP address the proxy issues the certificate from a certificate authority of its own, which every device that opens the ERP must trust (below). For a public name it obtains a certificate from Let's Encrypt instead, once ports 80 and 443 reach the server from the internet; no device then needs the proxy's root.

## Before the first start

The host bootstrap creates the proxy's volume once ([host setup runbook](host-setup.md)); by hand it is:

```bash
docker volume create erp-ai-pro-proxy-data
```

It holds the authority's root and intermediate with their keys and every certificate the proxy issued or obtained. Only root and the `docker` group, whose only member is the deploy account `erp-ai-pro`, can read it; the host bootstrap also keeps the `lxd` group, which would be root as well, empty. It is external, so `docker compose down -v` keeps it. Never remove it: the proxy would create a new root, and every device would have to trust that one instead.

## Exporting the root certificate

With the stack running, as `erp-ai-pro` from `/srv/erp-ai-pro/ERP-AI-Pro/infra/compose` on the host:

```bash
docker compose -f compose.yaml -f compose.production.yaml exec -T proxy cat /data/caddy/pki/authorities/local/root.crt > erp-proxy-root.crt
openssl x509 -in erp-proxy-root.crt -noout -subject -enddate -fingerprint -sha256
```

The subject is `Caddy Local Authority - <year> ECC Root` and the root is valid for ten years. `erp-proxy-root.crt` holds only the public certificate, so it may be copied to each device; compare the SHA-256 fingerprint shown on the device with the one printed here before trusting it. Never copy `root.key` or `intermediate.key` out of the volume: whoever holds the root key can make a certificate for any name that every trusting device accepts.

Each computer and phone that opens the ERP trusts this certificate once, as a trusted root certification authority; the [host setup runbook](host-setup.md), section "Trusting the proxy's root on each device", has the steps for Windows, macOS, iPhone and iPad, Android and Firefox. Until it does, the browser refuses the connection, and with it the session cookie, so sign-in fails.

## What the proxy enforces

`infra/proxy/Caddyfile` holds all of it, and the compose smoke test checks each point through the proxy on every build:

- **TLS.** TLS 1.3, and TLS 1.2 with ECDHE and AES-GCM or ChaCha20-Poly1305 only: Caddy's own defaults, which the Caddyfile leaves unchanged and the smoke test measures. A device that cannot speak TLS 1.2 cannot open the ERP.
- **Bodies.** Up to 101 MiB on `/api/*`, the largest upload plus room to spare, so the API answers every body it refuses itself. Up to 2 MiB on pages, and a larger one answers 413 with an empty body. Nothing is held back: uploads, downloads and the change history export stream through.
- **Time.** Reading a request's body and writing its answer may each take 11 minutes from the request's start, more than the API's own limits of at most 10 minutes. A connection that stalls for a minute is cut.
- **Other hosts.** A plain-HTTP request goes to `https://<ERP_SITE_ADDRESS>` with 308, whatever host it names, and an HTTPS request naming another host answers 421.
- **Headers.** No answer names the proxy (`Server`, `Via`), and HSTS is not sent while people open an IP address, because browsers ignore it there.

## Reading its log

As `erp-ai-pro` from `/srv/erp-ai-pro/ERP-AI-Pro/infra/compose` on the host:

```bash
docker compose -f compose.yaml -f compose.production.yaml logs --no-color --since 1h proxy
```

Each line is JSON:

- An entry whose `logger` begins with `http.log.access` is one request. It holds the device's address (`request.remote_ip`), `request.method`, `request.host`, the path (`request.uri`), the TLS version (`request.tls.version`, 771 for TLS 1.2 and 772 for TLS 1.3), `status`, `size`, `bytes_read` and `duration` in seconds.
- An entry whose `logger` begins with `http.log.error` names a failure, such as `dial tcp: lookup api` with status 502 while the api container is down.

Query strings and headers are never written, so a search for a person, a download link, a cookie or a token cannot be read from the log. The proxy logs to the server's journal under the tag `erp-ai-pro-proxy` (`journalctl CONTAINER_TAG=erp-ai-pro-proxy` as the administrator), which keeps it for more than a year, as CERT-In's direction (iv) of 28 April 2022 and the DPDP Rules, 2025 ask ([records runbook](records.md), [ADR-0058](../../adr/0058-records-kept-a-year-inside-india.md)).

## Moving to a public name

When the ERP gets a public name, set `ERP_AZURE_PRODUCTION_WEB_ORIGIN` to `https://<name>` and run `scripts/azure/entra.sh`, which registers the new redirect URIs and writes `Erp:Platform:Identity:WebOrigin`; change `Erp:Platform:Host:AllowedHosts` and `ERP_SITE_ADDRESS` to the name, let ports 80 and 443 reach the server, and roll the stack. The proxy obtains a Let's Encrypt certificate for the name, and the devices may remove the old root.

Then turn HSTS on in steps, each a change to the snippet `edge` of `infra/proxy/Caddyfile` shipped as a release:

1. `header Strict-Transport-Security "max-age=300"`.
2. A week (`max-age=604800`), once the first step has run without a certificate problem.
3. A month (`max-age=2592000`).
4. A year (`max-age=31536000`).

Add `includeSubDomains` only when every name under the domain serves HTTPS, and never `preload` without the owner's decision, because neither can be taken back quickly. A browser keeps the rule for its whole `max-age`, so for that long it refuses the name over plain HTTP.
