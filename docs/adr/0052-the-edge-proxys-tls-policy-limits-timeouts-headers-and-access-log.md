# ADR-0052: The edge proxy's TLS policy, limits, timeouts, headers and access log

Status: accepted
Date: 2026-10-19

## Context

- **What existed.** [ADR-0048](0048-the-edge-proxy-caddy-on-the-servers-local-address.md) put Caddy 2.11.7 in front of the stack: one site `{$ERP_SITE_ADDRESS}`, `/api/*` to `api:8080` and everything else to `web:3000`, HTTP redirected by Caddy's automatic HTTPS, HTTP/1.1 and HTTP/2, no access log. It left the TLS policy, the headers the proxy owns, the limits, an access log without query strings and the check through the proxy to `first-deployment-reverse-proxy-and-tls`.
- **What the upstreams accept.**
  - The API: Kestrel refuses a body over `Erp:Platform:Host:MaxRequestBodyBytes` (seeded 1 MiB, permitted 1 KiB to 1 GiB, [ADR-0018](0018-transport-security-and-the-request-pipeline-order.md)), and the attachments upload raises its own limit to `MaxSizeBytes` plus 64 KiB of multipart framing, `MaxSizeBytes` being at most `AttachmentsOptions.MaxSizeBytesCeiling`, 100 MiB ([ADR-0022](0022-attachments-in-azure-blob-storage.md)).
  - The API's timeouts are at most 10 minutes: `Erp:Platform:Host:RequestTimeout` and `Erp:Platform:Attachments:TransferTimeout` are both validated up to 10 minutes, and the change history export runs for up to five ([ADR-0043](0043-change-records-and-the-append-only-audit-schema.md)).
  - The web app: Next.js refuses a Server Function body over 1 MB by default ("By default, the maximum size of the request body sent to a Server Action is 1MB", nextjs.org, `serverActions`). Its standalone server also holds the body of every request `proxy.ts` matches in memory up to 10 MB and cuts the rest silently (`docs/architecture/request-pipeline.md`, measured on 2026-09-26).
  - Neither names its software: Kestrel runs with `AddServerHeader = false`, and `next.config.ts` sets `poweredByHeader: false`.
- **What query strings carry.**
  - A download link's token: `GET /api/platform/attachments/{id}/content?link=`.
  - An administrator's search for a person: `GET /api/identity/users/directory?search=` ([ADR-0040](0040-directory-invitations.md)).
  - The change history export's filter, which names records and actors.
  The API never logs them (`docs/guides/observability.md`), and the roadmap notes of those items asked the proxy's access log to leave every query string out.
- **Caddy 2.11.7.** These facts come from caddyserver.com ("reverse_proxy", "request_body", "log", "tls", "Global options") and the `v2.11.7` source on github.com/caddyserver/caddy, and were measured in the compose smoke stack on 2026-10-19.
  - TLS: "Default min: `tls1.2`, Default max: `tls1.3`", and the docs say "DO NOT change these unless you know what you're doing". The default TLS 1.2 suites (`defaultCipherSuitesWithAESNI`, `modules/caddytls/values.go`) are ECDHE with AES-GCM or ChaCha20-Poly1305 only. The default key exchanges are `X25519MLKEM768`, `X25519` and P-256.
  - Measured with the proxy's ECDSA certificate:
    - TLS 1.0 and 1.1 are refused with a protocol-version alert;
    - of the 57 TLS 1.2 suites Node.js's OpenSSL offers one at a time, Caddy accepts `ECDHE-ECDSA-AES128-GCM-SHA256`, `ECDHE-ECDSA-AES256-GCM-SHA384` and `ECDHE-ECDSA-CHACHA20-POLY1305` alone;
    - TLS 1.3 is accepted, and ALPN gives `h2` or `http/1.1`.
  - Server timeouts (`modules/caddyhttp/app.go`, `server.go`):
    - there is no hard deadline for reading a body or writing a response (`read_body`, `write`);
    - `read_header` is 1 minute and `idle` 5 minutes;
    - `read_body_idle` and `write_idle` are 1 minute, "reset on every successful read/write; only a stalled connection is affected";
    - `max_header_size` is 16 KiB, the same as Node.js's default `--max-http-header-size` for the web container, while Kestrel allows 32 KiB.
  - Towards an upstream there is no response-header, read or write timeout, and dialling waits 3 seconds.
  - Request and response bodies stream, because neither buffer is set; a response of unknown length, such as the export's CSV, is flushed at once.
  - `request_body` `max_size` wraps the body in `http.MaxBytesReader`: "Reads of more bytes will return an error with HTTP status `413`". It does not check `Content-Length` first.
  - Measured on the old configuration:
    - every answer carried `Server: Caddy`, and every proxied one `Via: 1.1 Caddy`;
    - Caddy's own error answers (413, 502) bypass a deferred header rule of the site;
    - with automatic redirects, a plain-HTTP request naming another host was redirected to that host;
    - an HTTPS request without a server name naming another host received an empty 200.
  - An access log records every request header, of which only `Cookie`, `Set-Cookie`, `Authorization` and `Proxy-Authorization` are redacted, every response header and the URI with its query string. Error entries name their request the same way: a 502 logged `request.uri` with its query.
- **Standards.**
  - A browser ignores HSTS for an IP address: "the UA MUST NOT note this host as a Known HSTS Host" (RFC 6797, section 8.1.1).
  - 421 Misdirected Request answers a request "directed at a server that is unable or unwilling to produce an authoritative response for the target URI" (RFC 9110, section 15.5.20).
  - A gateway "MAY send a Via header field in forwarded response messages" (RFC 9110, section 7.6.3).
- **Owner decision of 2026-10-19.** The scope asked for "a local end-to-end run through the proxy including the Entra redirect flow". A test cannot sign in with a real Microsoft account. Offered a stand-in for Microsoft inside the smoke test, the owner chose instead:
  - every sign-in step that needs no Microsoft runs through the proxy on every build;
  - the owner's real sign-in with a Microsoft account through the proxy happens on the server at go-live.

## Decision

- **TLS: Caddy's defaults, measured on every build.** The Caddyfile restates no protocol, suite or key exchange. `scripts/checks/compose-smoke.ts` connects to the proxy without a server name, trusting only its root, and requires:
  - TLS 1.0 and TLS 1.1 refused with the protocol-version alert;
  - every TLS 1.2 suite Node.js's OpenSSL offers, tried one at a time, refused unless it is ECDHE with AES-GCM or ChaCha20-Poly1305 (`forwardSecretAeadSuite`), with at least one accepted;
  - TLS 1.3 accepted;
  - ALPN negotiating `h2` when offered with `http/1.1`, and `http/1.1` alone.
  A Caddy release that weakened a default would fail the build.
- **Headers.**
  - The snippet `edge`, imported by every site, removes `Server` and `Via` from every answer: a deferred `header` rule covers the proxied answers, and `handle_errors` with `header -Server` and `respond "" {err.status_code}` covers Caddy's own errors, which keep their status and empty body.
  - No answer offers HTTP/3 (`Alt-Svc`), because only `h1 h2` are enabled.
  - The proxy sends no HSTS while the site is an IP address. The ramp-up for a public name is a roadmap note on `operations-hardening-security-posture-gates-and-checklist`.
  - The headers of the API and the web app pass unchanged: the proxy owns no other header (root `CLAUDE.md` §9).
- **Other hosts.**
  - `auto_https disable_redirects` turns Caddy's own redirects off. The site `http://` redirects every plain-HTTP request, whatever host it names, to `https://{$ERP_SITE_ADDRESS}{uri}` with 308.
  - The site `https://` answers 421 to an HTTPS request that names another host. Such a request arrives without a server name, because one with another server name fails the handshake.
  - A public name's ACME challenges still work: Caddy answers them before any route (`HandleHTTPChallenge`, `server.go`).
- **Request body limits** (`request_body`):
  - `/api/*`: 101 MiB, above the largest body any API route can be configured to accept: an upload of 100 MiB plus its framing, and `Erp:Platform:Host:MaxRequestBodyBytes`, whose permitted maximum becomes 100 MiB. The API therefore answers every body it refuses with its own problem.
  - Every other route: 2 MiB, above the web app's 1 MB Server Function limit and below the 10 MB its standalone server would cut silently. A larger body answers 413 with an empty body from the proxy.
  - No body is buffered.
- **Timeouts** (global `servers`):
  - `read_body` and `write` are 11 minutes from the start of each request, above the API's longest permitted timeout (10 minutes), so a slow upload, download or export ends with the API's own 504.
  - `read_body_idle` and `write_idle` are pinned at 1 minute, so a connection that stalls is cut.
  - Towards both upstreams, `response_header_timeout` and `read_timeout` are 11 minutes (snippet `upstream`).
  - `read_header` (1 minute), `idle` (5 minutes) and `max_header_size` (16 KiB) keep Caddy's defaults.
- **Access log.** Every site imports a `log` whose `filter` encoder writes JSON. It cuts `request>uri` at its first `?` (`regexp [?].* ""`) and deletes `request>headers` and `resp_headers`. The global default logger applies the same three filters, because it writes the error entries and the access entries of requests for another host. The rest of an entry stays:
  - the time, the level and the logger;
  - the client address and port;
  - the protocol, the method, the host and the path;
  - the TLS version and suite;
  - the bytes read, the duration, the size and the status.
  The entries go to the container's output, which `compose.production.yaml` rotates (20 MiB, five files).
- **Verification.**
  - The compose smoke test (`docker-build.yml` and `verify.ts --docker`) sends through the proxy:
    - the login page (its CSP), an anonymous API call (the API's `nosniff`) and the sign-in start's refusal of a return address on another site (400 `request.invalid`);
    - a made-up answer from Microsoft to `/api/auth/signin-oidc`, which the API answers with its redirect to `/login?error=sign-in-failed`;
    - the front-channel sign-out (200);
    - a 3 MiB form to the same callback, which reaches the API, and one to `/login`, which the proxy answers 413;
    - a directory search and a download link whose query strings carry random markers;
    - plain HTTP for the site's address and for another host (308 to the site's address), and HTTPS naming another host (421);
    - once the api is stopped, a request with a third marker, answered 502.
    It requires that no answer carries `Server`, `Via`, `Alt-Svc` or `Strict-Transport-Security`. It reads the image's own `caddy adapt` output and requires ceilings above 600 seconds, stall limits of 60 seconds, upstream timeouts above 600 seconds, `/api/*` limited above 100 MiB plus 64 KiB, and the other routes between 1 MiB and 10 MiB. In the proxy's log it requires no marker, no query string, no header and at least one access entry. The pure parts are tested in `scripts/checks/tests/compose-smoke.test.ts`.
  - The owner's real sign-in through the proxy, from a device that trusts the proxy's root, is part of `first-deployment-go-live-checklist-and-production-smoke` (owner decision of 2026-10-19).
  - The forwarded headers were already in place: the API trusts `X-Forwarded-For` and `X-Forwarded-Proto` only from `COMPOSE_NETWORK_CIDR`, `ForwardLimit` 1, and Caddy replaces any the client sent. Only 80 and 443 are published (ADR-0048, ADR-0049).
- **Earlier decisions this changes.**
  - ADR-0018: `Erp:Platform:Host:MaxRequestBodyBytes` is permitted from 1 KiB to 100 MiB, no longer to 1 GiB.
  - ADR-0048: the plain-HTTP redirect is the site `http://` instead of Caddy's automatic one, the proxy writes an access log, and of what ADR-0048 left to this item only HSTS remains, waiting for a public name.

## Options rejected

- **Restating the TLS defaults in the Caddyfile** (`protocols`, `ciphers`, `curves`). Caddy's documentation advises against changing them, and a fixed list would hold back improvements such as the post-quantum key exchange that 2.11.7 offers first. The measured check fails the build if a default ever weakens.
- **TLS 1.3 only.** It would refuse older phones and computers that may open the ERP, for little gain over TLS 1.2 with forward-secret AEAD suites alone; the scope asked for TLS 1.2 and above.
- **Proxy limits equal to the store's values** (1 MiB, and 25 MiB plus framing). The proxy cannot read App Configuration, so a copy baked into the image would disagree with the store after any change. A value in `.env` would be a setting outside the store, which the owner's rule of 2026-09-29 forbids. The ceilings above never disagree with a valid setting.
- **No limit on `/api/*`.** Kestrel refuses what the API's settings refuse, but the edge then would not stop a body that no route can ever accept.
- **HSTS now.** A browser must ignore it for an IP address (RFC 6797, section 8.1.1), so it would only look like protection.
- **A stand-in for Microsoft in the smoke test.** It would need a fake identity provider under `login.microsoftonline.com` inside the api container, trusted through a test certificate authority. `verify.ts --docker` would also add its made-up person to the developer's own database. The owner chose the real sign-in at go-live instead.
- **Keeping `Server: Caddy` and `Via: 1.1 Caddy`.** The API and the web app deliberately name no software, and the edge would undo that.
- **No access log.** Nothing would record who reached the server. CERT-In's direction (iv) of 28 April 2022 also asks for the logs of every ICT system, which the go-live checklist already lists for the edge proxy.

## Consequences

- **Body limits.**
  - A body over 2 MiB to a page is refused with an empty 413 by the proxy; nothing the web app sends comes near it, because uploads go to `/api`.
  - On `/api/*` a body up to 101 MiB reaches the API, which refuses what its settings refuse with its own 413 problem.
  - A `MaxRequestBodyBytes` above 100 MiB now stops the API at start.
- **Headers over 16 KiB** answer 431 at the edge. The web container's Node.js server refuses the same size, so pages would fail first.
- **Timeouts.** A connection that stops moving for a minute is cut. One that keeps moving runs until the API's own timeout, at most 10 minutes, and the proxy's 11-minute ceilings only catch an upstream that stops answering.
- **The access log.**
  - It keeps each device's address on the home network, the path and the outcome. Only the rotation of `compose.production.yaml` bounds it, so the 180 days of CERT-In's direction (iv) belong to the log retention of the go-live checklist, with the api's, the web container's and the database's logs.
  - Every line of the proxy's output is JSON, its start-up messages included.
- **Start-up warnings.** For the plain-HTTP server Caddy warns at start that no automatic HTTPS applies to it and that HTTP/2 needs TLS. Both are expected, because that server only redirects.
- **HSTS** waits for a public name. The [proxy runbook](../operations/runbooks/proxy.md) has the ramp-up, and the roadmap carries it.
- **A sign-in that breaks only through the proxy** would show up first at go-live. The smoke test covers every part the proxy touches without Microsoft: the routes, the callback's form body, the redirect, the front-channel sign-out and the headers.
