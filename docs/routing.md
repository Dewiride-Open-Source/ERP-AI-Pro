# Routing register

Every API route group and web route in the system. API routes follow `/api/<domain>/<module>/<resource>`; web routes mirror them without the `/api` prefix. Exempt prefixes: `/api/auth/*`, `/api/platform/*`, `/healthz/*`.

## Host endpoints

| Route | Auth | Purpose |
|---|---|---|
| `GET /healthz/live` | anonymous | process liveness (container health check) |
| `GET /healthz/ready` | anonymous | readiness including dependencies |
| `GET /openapi/erp.json` | anonymous, Development only | OpenAPI 3.1 document |
| `GET /scalar` | anonymous, Development only | API reference UI |
| `GET /api/platform/features` | anonymous until the authentication phase | every feature flag of the catalog with its evaluated state (`{ features: [{ name, enabled }] }`), read by the web shell to hide disabled modules |

Two conventions apply to the routes this register lists, from the module that first uses them: a route that creates something carries `.RequireIdempotencyKey()`, so the caller sends an `Idempotency-Key` header with a UUID, a repeat of the same request replays the first response with `Idempotency-Replayed: true`, and the same key with a different body answers 422; a route that lists something accepts `?page=&pageSize=&sort=&filter=` with per-route field allow-lists. Both grammars are in `docs/architecture/application-pipeline.md`. The routes below predate the conventions: `system-info/startups` returns a fixed 20 rows and no route creates anything yet.

## Modules

| Domain / Module | API group | Web routes | Notes |
|---|---|---|---|
| Platform / SystemInfo | `GET /api/platform/system-info`, `GET /api/platform/system-info/startups` | `/platform/system-info` | anonymous until the authentication phase; then requires an authenticated user; gated by `Erp.Modules.Platform.SystemInfo`. `startups` (route name `Platform.SystemInfo.ListStartups`) answers `200 application/json` `{ startups: [{ id, applicationName, version, framework, environmentName, configurationLabel (nullable), startedAt, recordedAt }] }`: the 20 most recent starts of the API, newest first (`startedAt` then `recordedAt` descending, ISO 8601 UTC); `MachineName` is stored and never returned while the route is anonymous. The web page shows the identity card and the recent-starts table |

Every module route group is gated by its module flag: when the flag is disabled the API answers `404 application/problem+json` with `code: feature.disabled` and `instance` set to the request path, the web shell hides the module's navigation entry, and the module's web segment renders the not-found page inside the shell with status 404.

## Web-only routes

| Route | Purpose |
|---|---|
| `/` | redirects to `/login` |
| `/login` | sign-in card (starts `/api/auth/login` once authentication exists) |
| `/healthz` | web container health check |
