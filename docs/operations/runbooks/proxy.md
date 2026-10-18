# Runbook: the edge proxy and its root certificate

The proxy is the only container that publishes ports (80 and 443, on IPv4 only, so the server's global IPv6 address reaches none of them): it serves the ERP over HTTPS at `ERP_SITE_ADDRESS`, sends `/api/*` to the api container and everything else to the web container. Design: [ADR-0048](../../adr/0048-the-edge-proxy-caddy-on-the-servers-local-address.md); the server itself: [host setup runbook](host-setup.md) ([ADR-0049](../../adr/0049-the-production-host-a-deploy-account-keys-from-the-home-network-and-nightly-updates.md)).

## The address

`ERP_SITE_ADDRESS` in `infra/compose/.env` on the host is the address people open the ERP at, without scheme or port: today the server's local address, such as `192.168.1.20`, which the router must keep fixed for the server. It is the host of `Erp:Platform:Identity:WebOrigin` under the production label, which `scripts/azure/entra.sh` writes from `ERP_AZURE_PRODUCTION_WEB_ORIGIN` (`https://<address>`) when it registers the production sign-in registration's redirect URIs, and the production `Erp:Platform:Host:AllowedHosts` starts with it (`<address>;api;127.0.0.1`), so the three always change together. The production stack refuses to start without it.

For an IP address the proxy issues the certificate from a certificate authority of its own, which every device that opens the ERP must trust (below). For a public name it obtains a certificate from Let's Encrypt instead, once ports 80 and 443 reach the server from the internet; no device then needs the proxy's root.

## Before the first start

The host bootstrap creates the proxy's volume once ([host setup runbook](host-setup.md)); by hand it is:

```bash
docker volume create erp-ai-pro-proxy-data
```

It holds the authority's root and intermediate with their keys and every certificate the proxy issued or obtained. Only root and the `docker` group, whose only member is the deploy account `erp-ai-pro`, can read it. It is external, so `docker compose down -v` keeps it. Never remove it: the proxy would create a new root, and every device would have to trust that one instead.

## Exporting the root certificate

With the stack running, as `erp-ai-pro` from `/srv/erp-ai-pro/ERP-AI-Pro/infra/compose` on the host:

```bash
docker compose -f compose.yaml -f compose.production.yaml exec -T proxy cat /data/caddy/pki/authorities/local/root.crt > erp-proxy-root.crt
openssl x509 -in erp-proxy-root.crt -noout -subject -enddate -fingerprint -sha256
```

The subject is `Caddy Local Authority - <year> ECC Root` and the root is valid for ten years. `erp-proxy-root.crt` holds only the public certificate, so it may be copied to each device; compare the SHA-256 fingerprint shown on the device with the one printed here before trusting it. Never copy `root.key` or `intermediate.key` out of the volume: whoever holds the root key can make a certificate for any name that every trusting device accepts.

Each computer and phone that opens the ERP trusts this certificate once, as a trusted root certification authority; the [host setup runbook](host-setup.md), section "Trusting the proxy's root on each device", has the steps for Windows, macOS, iPhone and iPad, Android and Firefox. Until it does, the browser refuses the connection, and with it the session cookie, so sign-in fails.

## Moving to a public name

When the ERP gets a public name, set `ERP_AZURE_PRODUCTION_WEB_ORIGIN` to `https://<name>` and run `scripts/azure/entra.sh`, which registers the new redirect URIs and writes `Erp:Platform:Identity:WebOrigin`; change `Erp:Platform:Host:AllowedHosts` and `ERP_SITE_ADDRESS` to the name, let ports 80 and 443 reach the server, and roll the stack. The proxy obtains a Let's Encrypt certificate for the name, and the devices may remove the old root.
