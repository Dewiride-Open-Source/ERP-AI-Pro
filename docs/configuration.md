# Configuration register

Every configuration key, feature flag and secret **name** in the system. Values never appear here. Keys follow `Erp:<Domain>:<Module>:<Setting>`; feature flags follow `Erp.Modules.<Domain>.<Module>[.<Capability>]`; secrets follow `Erp--<Domain>--<Module>--<Name>` (see ADR-0005).

## Bootstrap environment variables

| Variable | Used by | Purpose |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | API | `Development` or `Production` framework behaviour |
| `ASPNETCORE_HTTP_PORTS` | API container | listening port (8080 in containers) |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL` | API, web | OTLP exporter; telemetry export is off when the endpoint is unset (the API speaks gRPC, the web app OTLP/HTTP) |
| `OTEL_SERVICE_NAME` | web | service name for `@vercel/otel` |
| `API_INTERNAL_URL` | web | base URL of the API for server-side fetches and the runtime `/api/*` rewrite in `proxy.ts` |
| `NEXT_PUBLIC_APP_NAME` | web | display name in the shell |
| `E2E_BASE_URL`, `E2E_API_BASE_URL` | Playwright | target origins; when `E2E_BASE_URL` is unset Playwright starts the API and the web app itself |

### Compose interpolation (`infra/compose/.env`)

| Variable | Purpose |
|---|---|
| `IMAGE_TAG` | image tag pulled from ghcr.io (`local` for locally built images) |
| `APP_NAME` | value passed to the web app as `NEXT_PUBLIC_APP_NAME` |
| `COMPOSE_NETWORK_CIDR` | subnet of the compose network; the API trusts forwarded headers only from this range |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `WEB_OTEL_EXPORTER_OTLP_ENDPOINT` | local Aspire dashboard endpoints for the API (gRPC) and the web app (OTLP/HTTP) |

### Arriving with later phases

| Variable | Used by | Phase |
|---|---|---|
| `ERP_ENVIRONMENT` | API | App Configuration label (`local-dev` or `production`) — `azure-configuration` |
| `APPCONFIG_ENDPOINT` | API | Azure App Configuration endpoint; Production fails to start without it — `azure-configuration` |
| `AZURE_TOKEN_CREDENTIALS` | API | `dev` on developer machines (Azure CLI sign-in), `EnvironmentCredential` in the API container — `azure-configuration-api-configuration-bootstrap` |
| `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` | API container | tenant and application id of the runtime service principal the container signs in as — `azure-configuration-api-configuration-bootstrap` |
| `AZURE_CLIENT_CERTIFICATE_PATH` | API container | path of the runtime certificate secret file (`/run/secrets/erp-runtime-client.pem`) — `azure-configuration-api-configuration-bootstrap` |
| `ERP_TEST_SQL_CONNECTION` | integration tests | server-level SQL Server connection used to create per-run test databases — `backend-platform` |

## Configuration keys (`appsettings.json` / App Configuration)

| Key | Type | Default | Purpose |
|---|---|---|---|
| `Erp:Platform:Host:ApplicationName` | string | `ERP-AI-Pro` | name reported by the system-info endpoint and, from the `backend-platform` phase, the Data Protection application name and telemetry service name |
| `Erp:Platform:Host:AllowedHosts` | string | `*` | host filtering behind the edge proxy; narrowed to the public host name in the `first-deployment` phase |
| `Erp:Platform:Host:KnownNetworks` | string[] | `[]` | CIDR ranges trusted for forwarded headers (the compose network in production) |
| `Erp:Sentinel` | string | — | labelled `local-dev` and `production`; the value is the UTC timestamp of the last bump, and bumping it triggers a full configuration refresh in the API from sub-phase `azure-configuration-api-configuration-bootstrap` |
| `Erp:Platform:Identity:TenantId` | string | — | labelled `local-dev` and `production`; the Entra tenant id, written by `scripts/azure/entra.sh` (sub-phase `azure-configuration-entra-app-registration-scripts`) and bound by the `authentication` phase |
| `Erp:Platform:Identity:ClientId` | string | — | labelled `local-dev` and `production`; the application (client) id of that environment's sign-in registration, written by `scripts/azure/entra.sh` and bound by the `authentication` phase |

## Feature flags

| Flag | Purpose |
|---|---|
| `Erp.Modules.Platform.SystemInfo` | module flag name reserved for the system-info module; flag evaluation arrives with the `azure-configuration` phase |

## Secrets (Key Vault)

| Secret | Environment | Purpose |
|---|---|---|
| `Erp--Platform--Database--ConnectionString` | local-dev, production | SQL Server / Azure SQL connection string — `backend-platform` phase |
| `Erp--Platform--Identity--ClientCertificate` | local-dev, production | PKCS#12 certificate with private key of that environment's sign-in registration, created by `scripts/azure/entra.sh`; referenced by `Erp:Platform:Identity:ClientCertificate` from sub-phase `azure-configuration-configuration-conventions-and-seed-data` |
| `Erp--Platform--Identity--RuntimeClientCertificate` | production | PEM certificate with private key of the runtime service principal, created by `scripts/azure/entra.sh`; exported by hand with `scripts/azure/export-runtime-certificate.sh` to the server's compose secret file; never an App Configuration reference |

## Keys (Key Vault)

| Key | Environment | Purpose |
|---|---|---|
| `Erp--Platform--DataProtection--Key` | local-dev, production | RSA 2048 key with `wrapKey`/`unwrapKey`; protects the ASP.NET Core Data Protection key ring from the `authentication` phase |
