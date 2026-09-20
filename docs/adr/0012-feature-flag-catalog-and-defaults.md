# ADR-0012: Feature flag catalog and defaults

Status: accepted
Date: 2026-09-20

## Context

ADR-0005 names one feature flag per module (`Erp.Modules.<Domain>.<Module>`) plus capability flags (`Erp.Modules.<Domain>.<Module>.<Capability>`), all read from App Configuration under the environment label, and every AI capability must sit behind its own flag. The store is seeded from `infra/appconfig/feature-flags.json`, but tests, CI, the Playwright servers, the compose smoke and a freshly provisioned store hold no flag at all, and the API must still serve. `Microsoft.FeatureManagement` evaluates a flag that no provider defines as disabled, so a plain integration of the package would switch every module off wherever the store is absent. The package also offers `Microsoft.FeatureManagement.AspNetCore`, whose `WithFeatureGate` answers a disabled endpoint with a bodiless 404, which contradicts the ProblemDetails-everywhere rule.

## Decision

- `FeatureCatalog` (BuildingBlocks.Modules/Features) derives the flags from the `ModuleCatalog`: one module flag per registered module and one capability flag per `ModuleCapability` the module descriptor declares (`Name` in PascalCase, `EnabledByDefault`). A module flag defaults to enabled; a capability flag defaults to what its declaration says. Names are unique case-insensitively and never contain `:`.
- `CatalogFeatureDefinitionProvider` is the `IFeatureDefinitionProvider`: it asks the package's `ConfigurationFeatureDefinitionProvider` first (with `CustomConfigurationMergingEnabled`, so a definition from a later configuration provider overrides an earlier one by id, which makes App Configuration beat environment variables and a test override beat both) and falls back to the catalog default when configuration holds nothing for the flag. Root-configuration fallback stays off; a flag outside the catalog and outside configuration evaluates disabled. That precedence holds only among Microsoft-schema definitions (`feature_management:feature_flags`), because the package consults the Microsoft schema before the `.NET` schema (`FeatureManagement:<name>`) regardless of provider order; the App Configuration provider maps a plain flag to the `.NET` schema unless `AZURE_APP_CONFIGURATION_FM_SCHEMA_COMPATIBILITY_DISABLED=true`, so `AddErpConfiguration` sets that process variable before the store is connected (`AppConfigurationSetup.RequireMicrosoftFeatureFlagSchema`) — the provider offers no other switch — and every flag takes part in the last-provider-wins order.
- Every module route group carries `RequireFeature(descriptor.FeatureFlag)`, endpoint metadata that `FeatureGateMiddleware` (after routing, before the endpoint runs) evaluates per request: a disabled flag is answered with `404 application/problem+json`, `code: feature.disabled`, `instance` set to the request path, before any parameter binding, body deserialisation or handler construction happens — an endpoint filter would run only after those. `GET /api/platform/features` lists every catalog flag with its evaluated state for the web app; it is anonymous until the authentication phase adds the fallback policy and is whitelisted in the architecture test.
- The web app reads the list once per request (`shared/feature-flags/queries.ts`, `React.cache`), hides navigation entries whose `featureFlag` is disabled and renders the in-shell not-found page for a disabled module's segment (`requireFeature` in the segment layout). Reading the list fails open to an empty map: the API gates its own routes, so a web instance that cannot reach the API keeps rendering instead of hiding everything.
- Tests set flags through `ErpApiFactory.WithFeature(name, enabled)`, which writes the Microsoft schema (`feature_management:feature_flags:<n>:{id,enabled}`) as host settings; Playwright proves the disabled state against a second API and web pair started with `feature_management__feature_flags__0__id` and `__enabled` environment variables.
- `Microsoft.FeatureManagement.AspNetCore` is not referenced.

## Consequences

- A newly registered module is live everywhere until someone switches it off in the store; switching it off under a label is one `feature-flags.json` change plus `seed.sh`, and a flag not yet seeded cannot take a module down.
- A capability that must ship dark is declared with `EnabledByDefault: false` and switched on per label.
- The override schema is the package's Microsoft schema, so the same keys work for environment variables (`feature_management__feature_flags__0__id`), `appsettings.json`, App Configuration feature flags and tests.
- Because the web app fails open, a module hidden by the API is still hidden (its routes answer 404), but a web instance without the API shows every navigation entry; the API remains the single authority.
