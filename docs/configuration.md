# Configuration register

Every configuration key, feature flag and secret **name** in the system. Values never appear here. Keys follow `Erp:<Domain>:<Module>:<Setting>`; feature flags follow `Erp.Modules.<Domain>.<Module>[.<Capability>]`; secrets follow `Erp--<Domain>--<Module>--<Name>` (see ADR-0005).

## Bootstrap environment variables

| Variable | Used by | Purpose |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | API | `Development` or `Production` framework behaviour |
| `ASPNETCORE_HTTP_PORTS` | API container | listening port (8080 in containers) |
| `ERP_ENVIRONMENT` | API | App Configuration label, `local-dev` or `production`; required, and validated, whenever `APPCONFIG_ENDPOINT` is set (`local-dev` from `launchSettings.json` on developer machines, `production` from `compose.production.yaml`) |
| `APPCONFIG_ENDPOINT` | API | Azure App Configuration endpoint (`https://<store>.azconfig.io`); when set the API loads the store and its Key Vault references, when empty a Development host runs on `appsettings.json` plus user secrets and a Production host refuses to start; developers keep it in `dotnet user-secrets`, never in a committed file |
| `ERP_CONFIGURATION_SOURCE` | integration tests, `dotnet ef` | bootstrap value with two settings, never set by hand: `InMemory`, set by `ErpApiFactory` through `UseSetting` so every test host, Development or Production, runs on in-memory configuration without Azure; `LocalDevelopment`, set by the host itself (`ErpHostComposition.AddErpPlatform`) when `EF.IsDesignTime` is true, before `AddErpConfiguration` runs, so `dotnet ef` never contacts the store and reads `appsettings.json`, user secrets and the environment in any `ASPNETCORE_ENVIRONMENT` |
| `ERP_TEST_SQL_CONNECTION` | integration tests (`SqlTestDatabase`) | server-level connection string with rights to create databases and no `Database=` part; the assembly fixture creates `ErpAiProTest_<yyyyMMddHHmmss>_<8 hex>` from it per test process, migrates every catalogue context and drops it at the end, and fails with a message naming the variable when it is missing; on the owner's machine the Windows sign-in string `Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True` as a user environment variable, in CI the `server-connection-string` output of `.github/actions/start-sql-server` |
| `AZURE_TOKEN_CREDENTIALS` | API | mandatory selector for `DefaultAzureCredential`: `AzureCliCredential` on developer machines (from `launchSettings.json`; only the `az login` session is used, never a Visual Studio or VS Code sign-in) and `EnvironmentCredential` in the api container (from `compose.production.yaml`); outside `Development` the credential factory rejects any other value at startup |
| `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` | API container | tenant and application (client) id of the runtime service principal the container signs in as; both required in Production |
| `AZURE_APP_CONFIGURATION_FM_SCHEMA_COMPATIBILITY_DISABLED` | API (set by the API itself) | `true`, set by `AddErpConfiguration` in the process environment before the store is connected so the provider emits every feature flag in the Microsoft schema (ADR-0012); never set by hand |
| `AZURE_CLIENT_CERTIFICATE_PATH` | API container | path of the runtime certificate secret file, `/run/secrets/erp-runtime-client.pem`, which the credential factory opens for reading at startup so an unreadable file fails with a message naming the fix; must name an existing `.pem` or `.pfx` file in Production, and `AZURE_CLIENT_SECRET` must not be set alongside it |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL` | API, web | OTLP exporter; telemetry export is off when the endpoint is unset. The API speaks gRPC. The web app validates both: the endpoint is an `http` or `https` base URL (a path prefix is allowed, credentials are not, trailing slashes are stripped) and the protocol is `http/protobuf` (default) or `http/json`; the web app builds its trace exporter from the validated values (`<endpoint>/v1/traces`) and consults no other `OTEL_EXPORTER_*` variable |
| `OTEL_SERVICE_NAME` | web | service name for `@vercel/otel`; optional, default `erp-ai-pro-web`, 1–120 characters |
| `NODE_ENV` | web | `development`, `production` or `test` (default `development`); `next start` and the standalone server set `production`, which makes `API_INTERNAL_URL` mandatory |
| `API_INTERNAL_URL` | web | origin of the API (`scheme://host[:port]`, no path, query, fragment or credentials; normalised to the origin, so `http://api:8080/` becomes `http://api:8080`) for server-side fetches (`apiFetch`) and the runtime `/api/*` and `/openapi/*` rewrite in `proxy.ts`; single-label hosts such as `http://api:8080` accepted; required when `NODE_ENV=production`, default `http://localhost:5080` in development. Every web variable in this table is validated by `src/shared/config/env.schema.ts`: `next start` and the standalone server exit with a message naming the offending variable (`scripts/checks/web-startup-guard.ts` proves it after every build), `next build` succeeds without it |
| `NEXT_PUBLIC_APP_NAME` | web (build time) | display name in the shell and the browser title, 1–60 characters, default `ERP-AI-Pro`; read by `next.config.ts` when `next build` runs and inlined into the bundle, so at runtime the variable has no effect — the image takes it as a build argument (table below) |
| `E2E_BASE_URL`, `E2E_API_BASE_URL` | Playwright | target origins; when `E2E_BASE_URL` is unset Playwright starts the API and the web app itself |
| `E2E_OFFLINE_BASE_URL` | Playwright | origin of the web instance whose API is unreachable (port 3001 when Playwright starts it), for the API-unavailable states |
| `E2E_GATED_BASE_URL`, `E2E_GATED_API_BASE_URL` | Playwright | origins of the web and API pair started with `Erp.Modules.Platform.SystemInfo` disabled (ports 3002 and 5081 when Playwright starts them), for the disabled-module states |

### Compose interpolation (`infra/compose/.env`)

| Variable | Purpose |
|---|---|
| `IMAGE_TAG` | image tag pulled from ghcr.io (`local` for locally built images) |
| `COMPOSE_NETWORK_CIDR` | subnet of the compose network; the API trusts forwarded headers only from this range |
| `APPCONFIG_ENDPOINT` | passed to the API as `APPCONFIG_ENDPOINT`; required by the production stack, which refuses to start without it; left empty on a developer machine because the local stack cannot sign in to Azure and runs on `appsettings.json` (`dotnet run` uses user secrets instead) |
| `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` | production stack only; passed to the api container as the runtime service principal's tenant and application (client) id (ids, never secrets; the certificate is the compose secret `erp-runtime-client.pem` from `infra/compose/secrets/`) |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `WEB_OTEL_EXPORTER_OTLP_ENDPOINT` | local Aspire dashboard endpoints for the API (gRPC) and the web app (OTLP/HTTP) |

### Compose secret files (`infra/compose/secrets/`, git-ignored)

A compose secret is a file, never an `.env` variable; the api container reads the directory as `/run/secrets` through the key-per-file provider, which turns `__` into `:`.

| File | Stack | Purpose |
|---|---|---|
| `Erp__Platform__Database__ConnectionString` | local (`compose.override.yaml`), the `docker-build` smoke | lands on `Erp:Platform:Database:ConnectionString`; on the owner's machine `Server=host.docker.internal,1433;Database=ErpAiPro;User ID=<login>;Password=<password>;Encrypt=True;TrustServerCertificate=True`, a SQL login because a container cannot use Windows sign-in; the compose smoke script refuses to start without the file and names it |
| `erp-runtime-client.pem` | production (`compose.production.yaml`) | the runtime service principal's certificate, hidden from the key-per-file provider and read only through `AZURE_CLIENT_CERTIFICATE_PATH` |

### Image build arguments (`infra/docker/web.Dockerfile`)

| Argument | Default | Purpose |
|---|---|---|
| `NEXT_PUBLIC_APP_NAME` | `ERP-AI-Pro` | inlined display name; pass `--build-arg NEXT_PUBLIC_APP_NAME=...` to `docker build` (or `build.args` in a compose override) to rename the product in a custom image |

## Configuration keys (`appsettings.json` / App Configuration)

| Key | Type | Default | Purpose |
|---|---|---|---|
| `Erp:Platform:Host:ApplicationName` | string | `ERP-AI-Pro` | name reported by the system-info endpoint and, from the `backend-platform` phase, the Data Protection application name and telemetry service name; seeded unlabelled from `infra/appconfig/defaults.json` and overridden to `ERP-AI-Pro (local-dev)` under the `local-dev` label, which makes the environment visible on the system page and doubles as the label-precedence proof of `verify.sh --labels` |
| `Erp:Platform:Host:AllowedHosts` | string | `*` | host filtering behind the edge proxy; seeded unlabelled from `infra/appconfig/defaults.json` and narrowed to the public host name under the `production` label in the `first-deployment` phase |
| `Erp:Platform:Host:KnownNetworks` | string[] | `[]` | CIDR ranges trusted for forwarded headers (the compose network in production); host-local, set through `Erp__Platform__Host__KnownNetworks__0` in the compose file and never seeded in the store |
| `Erp:Platform:Configuration:RefreshInterval` | TimeSpan | `00:30:00` | how often the API checks the labelled `Erp:Sentinel` and the feature flags (between 1 second and 1 day); bootstrap-only: `appsettings.json`, environment or user secrets; read before the store is connected, not refreshable, never seeded in the store |
| `Erp:Platform:Configuration:SecretRefreshInterval` | TimeSpan | `01:00:00` | how long a resolved Key Vault reference is cached before the secret is read again (between 1 minute and 7 days); bootstrap-only: `appsettings.json`, environment or user secrets; read before the store is connected, not refreshable, never seeded in the store |
| `Erp:Platform:Configuration:StartupTimeout` | TimeSpan | `00:01:00` | how long the first load from the store may take before startup fails (between 1 second and 10 minutes); bootstrap-only: `appsettings.json`, environment or user secrets; read before the store is connected, not refreshable, never seeded in the store |
| `Erp:Platform:Database:ConnectionString` | Key Vault reference | — | connection string of the one ERP database, required at run time and optional at EF design time; labelled `local-dev`, resolving to `Erp--Platform--Database--ConnectionString` in the local-dev vault (`infra/appconfig/key-vault-references.json` entry with `"labels": ["local-dev"]`); the `production` reference is added by the first-deployment phase once the production secret exists. Read per process: `dotnet run` with the store through the reference; `dotnet ef` and a store-less Development run from `dotnet user-secrets` of `Hosts/Api/Dewiride.Erp.Host.Api`; CI from the environment variable `Erp__Platform__Database__ConnectionString`; containers from the key-per-file secret `/run/secrets/Erp__Platform__Database__ConnectionString`; tests from `SqlTestDatabase` through `ErpApiFactory`, which overrides every other source |
| `Erp:Platform:Database:Provider` | `DatabaseProvider` (`SqlServer` or `AzureSql`) | `SqlServer` | `SqlServer` selects `UseSqlServer` with compatibility level 170, `AzureSql` selects `UseAzureSql`, both with the same history table, timeout and retry settings; seeded unlabelled as `SqlServer` from `infra/appconfig/defaults.json` and overridden to `AzureSql` under the `production` label from `infra/appconfig/production.json` |
| `Erp:Platform:Database:CommandTimeout` | TimeSpan | `00:00:30` | command timeout on every module context (between 1 second and 10 minutes); seeded unlabelled from `infra/appconfig/defaults.json` |
| `Erp:Platform:Database:MaxRetryCount` | int | `5` | `EnableRetryOnFailure` retry count (0 to 20); seeded unlabelled from `infra/appconfig/defaults.json` |
| `Erp:Platform:Database:MaxRetryDelay` | TimeSpan | `00:00:10` | `EnableRetryOnFailure` maximum delay between retries (between 1 second and 2 minutes); seeded unlabelled from `infra/appconfig/defaults.json` |
| `Erp:Sentinel` | string | — | labelled `local-dev` and `production`; the value is the UTC timestamp of the last bump, and bumping it triggers a full configuration refresh in the API at the next check |
| `Erp:Platform:Identity:TenantId` | string | — | labelled `local-dev` and `production`; the Entra tenant id, written by `scripts/azure/entra.sh` (sub-phase `azure-configuration-entra-app-registration-scripts`) and bound by the `authentication` phase |
| `Erp:Platform:Identity:ClientId` | string | — | labelled `local-dev` and `production`; the application (client) id of that environment's sign-in registration, written by `scripts/azure/entra.sh` and bound by the `authentication` phase |
| `Erp:Platform:Identity:Instance` | string | `https://login.microsoftonline.com/` | Entra authority the sign-in redirects to; seeded unlabelled from `infra/appconfig/defaults.json` and bound by the `authentication` phase |
| `Erp:Platform:Identity:ClientCertificate` | Key Vault reference | — | labelled `local-dev` and `production`; resolves to `Erp--Platform--Identity--ClientCertificate` in that environment's vault (URI composed by `scripts/azure/seed.sh` from `infra/appconfig/key-vault-references.json`), the PKCS#12 sign-in certificate the `authentication` phase presents to Entra |

## Feature flags

| Flag | Purpose |
|---|---|
| `Erp.Modules.Platform.SystemInfo` | module flag of the system-info module; seeded from `infra/appconfig/feature-flags.json` as enabled under `local-dev` and `production` |

Evaluation (ADR-0012): the `FeatureCatalog` derives one flag per registered module and one per declared `ModuleCapability`; a module flag that no configuration source defines evaluates enabled, a capability flag follows its declared default, and a definition from a later configuration source overrides an earlier one by id (App Configuration beats environment variables, which beat `appsettings.json`; the API runs the store with `AZURE_APP_CONFIGURATION_FM_SCHEMA_COMPATIBILITY_DISABLED=true`, set by `AddErpConfiguration` itself, so store flags use the same Microsoft schema as every other source and that order holds). Any source may define a flag with the Microsoft schema — as environment variables `feature_management__feature_flags__<n>__id=Erp.Modules.<Domain>.<Module>` and `feature_management__feature_flags__<n>__enabled=true|false`, as the same keys in `appsettings.json`, or as App Configuration feature flags; production values come from App Configuration. A disabled module flag makes every route of the module answer `404 application/problem+json` with `code: feature.disabled`, hides the module in the web shell and turns its web segment into the in-shell not-found page. `GET /api/platform/features` reports every catalog flag with its evaluated state.

## Secrets (Key Vault)

| Secret | Environment | Purpose |
|---|---|---|
| `Erp--Platform--Database--ConnectionString` | local-dev; production from the first-deployment phase | connection string of the ERP database for host processes, referenced by `Erp:Platform:Database:ConnectionString` and written by `scripts/azure/seed.sh` only under the labels the reference lists; local-dev: the Windows sign-in string `Server=localhost;Database=ErpAiPro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True` (no credential inside), created with `az keyvault secret set --file` in the session that introduces the key, with the owner's consent; production: the Azure SQL string with `Authentication="Active Directory Default";Encrypt=True;TrustServerCertificate=False`, created in the first-deployment phase |
| `Erp--Platform--Identity--ClientCertificate` | local-dev, production | PKCS#12 certificate with private key of that environment's sign-in registration, created by `scripts/azure/entra.sh`; referenced by `Erp:Platform:Identity:ClientCertificate`, written by `scripts/azure/seed.sh` only after the secret exists |
| `Erp--Platform--Identity--RuntimeClientCertificate` | production | PEM certificate with private key of the runtime service principal, created by `scripts/azure/entra.sh`; exported by hand with `scripts/azure/export-runtime-certificate.sh` to the server's compose secret file; never an App Configuration reference |

## Keys (Key Vault)

| Key | Environment | Purpose |
|---|---|---|
| `Erp--Platform--DataProtection--Key` | local-dev, production | RSA 2048 key with `wrapKey`/`unwrapKey`; protects the ASP.NET Core Data Protection key ring from the `authentication` phase |
