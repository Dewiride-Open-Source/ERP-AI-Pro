# Routing register

Every API route group and web route in the system. API routes follow `/api/<domain>/<module>/<resource>`; web routes mirror them without the `/api` prefix. Exempt prefixes: `/api/auth/*`, `/api/platform/*`, `/healthz/*`.

## Host endpoints

| Route | Auth | Purpose |
|---|---|---|
| `GET /healthz/live` | anonymous | process liveness (container health check) |
| `GET /healthz/ready` | anonymous | readiness including dependencies |
| `GET /openapi/erp.json` | anonymous, Development only | OpenAPI 3.1 document |
| `GET /scalar` | anonymous, Development only | API reference UI |

## Modules

| Domain / Module | API group | Web routes | Notes |
|---|---|---|---|
| Platform / SystemInfo | `GET /api/platform/system-info` | `/platform/system-info` | anonymous until the authentication phase; then requires an authenticated user |

## Web-only routes

| Route | Purpose |
|---|---|
| `/` | redirects to `/login` |
| `/login` | sign-in card (starts `/api/auth/login` once authentication exists) |
| `/healthz` | web container health check |
