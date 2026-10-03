# ADR-0034: Session endpoints, bearer tokens per route and the security description

Status: accepted
Date: 2026-10-03

## Context

Sub-phase `authentication-session-endpoints-openapi-security-and-bearer` (Community edition, confirmed 2026-09-29) adds `GET /api/auth/me` and a session-ping endpoint, describes the session cookie and the antiforgery header as security schemes of the OpenAPI document, and registers a JWT bearer scheme for later integrations and a native app, behind a feature flag, with per-group scheme selection, scope and app-role checks and a test token issuer. ADR-0031 left a cookie 401 without `WWW-Authenticate` and asked that a bearer scheme keep 401 and 403 problems without `Location`; ADR-0033 checks a request for its antiforgery token when `AuthenticateAsync("Cookies")` succeeds, and noted that a request carrying both a bearer token and the session cookie would be checked against `HttpContext.User`.

Facts that shape the design, read on 2026-10-03 in dotnet/aspnetcore v10.0.12, AzureAD/microsoft-identity-web 4.15.0, AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet 8.23.0, microsoft/OpenAPI.NET v2.12.0 and microsoft/kiota v1.35.0, and in the Microsoft Learn pages "Access token claims reference", "Protected web API: Verify scopes and app roles", "Secure applications and APIs by validating claims" and "Customize OpenAPI documents":

- The app registration is both the web sign-in client and the API resource. Its `api.requestedAccessTokenVersion` is 2, so Entra issues it only v2.0 access tokens, whose `aud` is the client id and whose `iss` is `https://login.microsoftonline.com/{tid}/v2.0`. It has no Application ID URI, no delegated scope and no application role yet, so no client can obtain an access token for it today.
- A person's access token carries the granted scopes in `scp` and the person's app roles in `roles`. An application's own token carries no `scp` and its application roles in `roles`; it says `app` in `idtyp` only when the registration emits that optional claim, and its `sub` equals its `oid`. An id token of the same registration has the same `aud`, `iss` and signing keys, but neither `scp` nor a `sub` equal to its `oid`.
- Microsoft.Identity.Web 4.15.0's `AddMicrosoftIdentityWebApi` installs `AadIssuerValidator`, which accepts a v1.0 token of the tenant after downloading the v1.0 metadata outside `IHttpClientFactory`, and `RegisterValidAudience`, which accepts `api://{ClientId}` for a v1.0 token. Its token check requires only that `scp` or `roles` be present, so an id token that carries roles passes. It adds `ScopeAuthorizationRequirement` and `ScopeOrAppPermissionAuthorizationRequirement` to `AuthorizationOptions.DefaultPolicy` for every scheme, and its `RequireScopeOrAppPermission` accepts any matching `roles` value, a person's own included.
- `JwtBearerOptions.MapInboundClaims` defaults to true, and `IncludeErrorDetails` to true, which writes the validation failure into `WWW-Authenticate`. Without an `IssuerValidator` the handler compares the issuer ordinally with `ValidIssuer` and the metadata issuer.
- The authentication middleware authenticates only the default scheme. A policy that names schemes replaces `HttpContext.User` during authorization, after the rate limiter, which partitions signed-in callers by `IActorContext`. A policy that names two schemes challenges both on one response, and the cookie's problem body starts the response before the bearer challenge sets its header.
- The cookie handler raises `CheckSlidingExpiration` from `HandleAuthenticateAsync`, when the routed endpoint is already known, renews with the ticket's own span from the time it authenticated, and keeps `IssuedUtc` and `ExpiresUtc` to the second, because `AuthenticationProperties` stores them in the `r` format.
- In Microsoft.OpenApi 2.12.0 a security requirement whose `OpenApiSecuritySchemeReference` has no host document, or names an undeclared scheme, is written as `{}`, which OpenAPI 3.1 reads as "anonymous access is allowed". Operation transformers run before document transformers. Kiota 1.35.0 ignores security when it generates TypeScript.
- `Microsoft.FeatureManagement` logs a warning on every evaluation of a flag that no provider defines.

## Decision

- **Who is signed in.** `GET /api/auth/me` (`Auth.Me`, signed in under the fallback policy) answers `CurrentUserResponse { id, name, userName, roles }` from the session's claims `oid`, `name`, `preferred_username` and every `roles` value. A principal without them fails the request with 500, because every Entra sign-in carries them. The person's permissions join the response with `user-management-roles-and-permissions`; until then it has no permissions member rather than an empty placeholder. The sign-in name is `userName`, not `email`: managed accounts carry `email` only as an optional claim of the registration, and `preferred_username` is the name the person signs in with.
- **The session, read and renewed.** `GET /api/auth/session` (`Auth.Session`) and `POST /api/auth/session` (`Auth.RenewSession`) require the session cookie itself, through a policy naming `Cookies`, so the test header and a bearer token, which have no session, answer 401. Both answer `SessionResponse { expiresAt, lifetimeEndsAt }`: `lifetimeEndsAt` is `erp.signed-in-at` plus `SessionLifetime`, and `expiresAt` is the cookie's expiry, never later than `lifetimeEndsAt`. The GET never renews the cookie and the POST always does, through the endpoint metadata `SessionRenewalMetadata` that `SessionCookieEvents.CheckSlidingExpiration` reads; every other request keeps the renewal interval of ADR-0031. A page can therefore watch the time left without keeping an idle person signed in, and renew the session when the person chooses to stay. The POST computes the renewed expiry as the handler does, from now and the ticket's span, to the second. A POST refused for its antiforgery token still renews the cookie, as any request past the renewal interval does; that extends a session and changes nothing else.
- **Bearer tokens use the framework's handler.** The scheme is `AddJwtBearer("Bearer")` from `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12 (pre-approved, already pinned), not the `AddMicrosoftIdentityWebApi` the scope named, whose defaults accept a v1.0 token of the tenant and change the default policy for every scheme. `BearerTokenOptionsSetup` sets:
  - `Authority` to the tenant's v2.0 authority, `Erp:Platform:Identity:Instance` + `TenantId` + `/v2.0`, whose metadata gives the signing keys;
  - `ValidAudience` to the client id, and `ValidIssuer` to that authority, compared exactly by the handler;
  - `ValidAlgorithms` to `RS256`;
  - `MapInboundClaims`, `IncludeErrorDetails` and `SaveToken` to false;
  - the name claim `preferred_username` and the role claim `roles`;
  - `EventsType` to `BearerTokenEvents`.

  `BearerTokenEvents.TokenValidated` also refuses a token in any of these cases:
  - its `ver` is not `2.0`;
  - its `tid` is not the tenant;
  - its `oid` is not a GUID;
  - it has no holder (`BearerTokenClaims.HolderOf`). The holder is a person (`scp`, with `idtyp` absent or `user`) or an application (no `scp`, with `idtyp` `app`, or no `idtyp` and a `sub` equal to its `oid`). This leaves out the id tokens of the same registration.

  The scheme adds no setting: it reads `Erp:Platform:Identity`.
- **Off unless switched on.** The platform flag `Erp.Platform.Identity.BearerTokens` is disabled by default and seeded disabled under both labels. It is evaluated on every request. While it is off, `BearerTokenEvents.MessageReceived` answers no result, so no token signs anything in, and the challenge names no scheme. A building block declares such a flag with `AddErpPlatformFeature`, which adds it to the `FeatureCatalog` with its default under the name `Erp.Platform.<Concern>.<Capability>`, beside the module flags of ADR-0012. Evaluating it then meets a definition, and `GET /api/platform/features` lists it.
- **The route decides the scheme.** The default authenticate scheme is the policy scheme `Erp.RouteSignIn` (`RouteSignInScheme`). Its `ForwardDefaultSelector` picks `Bearer` for an endpoint carrying `BearerTokenRouteMetadata`, and `Cookies` for every other endpoint and for a request that matches no route. The choice is made in the authentication middleware, before the rate limiter. So a bearer caller is limited as the person or application its token names (ADR-0019), and the cookie handler never runs on a bearer route, where it would slide, check or clear the session of a browser that happens to send its cookie. `RequireBearerToken(BearerTokenAccess)` is the only way a route takes bearer tokens: it adds that metadata and a policy naming `Bearer` alone with `BearerTokenAccessRequirement`. A route never takes both the cookie and a token, because a policy naming both would challenge both on one response; `AuthenticationSchemeTests.Endpoints_NameNoSchemeButTheOneTheirRouteSignsInWith` fails on an endpoint that names any other scheme. Every other route takes the session cookie alone and ignores a bearer token.
- **Scopes for people, roles for applications.** `BearerTokenAccess` lists the scopes that grant a person's token and the application roles that grant an application's: at least one name, each one word. `BearerTokenAccessHandler` checks a person's token by its scopes alone and an application's by its roles alone, because a person's `roles` are their own app roles. A token without the access answers 403. Microsoft.Identity.Web's `RequireScopeOrAppPermission` is not used, because it would accept a person's roles as application permissions. The roles a route lists must be app roles that only applications can be assigned.
- **Refusals.** `BearerTokenEvents.Challenge` answers 401 `request.unauthenticated` with:
  - `WWW-Authenticate: Bearer` when no token was sent;
  - `Bearer error="invalid_token"`, never the reason (RFC 6750 section 3.1), when one was refused;
  - no `WWW-Authenticate` while the flag is off.

  `Forbidden` answers 403 `request.forbidden` with `Bearer error="insufficient_scope"`. Both write their problem through `AuthenticationProblems`, which the session cookie uses too. Its 403 title is now "The caller may not do this.", because a caller may be an application. A cookie 401 still carries no `WWW-Authenticate`: the cookie is no HTTP authentication scheme, so a session route has none to name.
- **Antiforgery follows the route.** `AntiforgeryValidationMiddleware` skips a route that takes bearer tokens without running the cookie handler. Otherwise it checks a POST, PUT, PATCH or DELETE exactly when the session cookie signed the request in: the cookie authenticates, and one of its identities is one of `HttpContext.User`'s. That holds because the handler authenticates once per request and the user is built from the same identity instances. A request signed in by the test header while a cookie travels along passes unchecked, like one without the cookie. A request to a session route that carries a bearer token and the cookie is checked, because the cookie signs it in.
- **The security description.** `AddErpSecurityDescription` (`BuildingBlocks.Authentication/OpenApi`), called by the host's `AddOpenApi("erp", …)`, adds `SecurityRequirementsOperationTransformer` and `SecuritySchemesDocumentTransformer`. Each operation's requirement comes from its metadata alone, never from the registered schemes or the flag, because the test host that writes the snapshot registers its own default scheme:

  | Operation | Requirement |
  |---|---|
  | anonymous GET | `[]` |
  | anonymous POST, PUT, PATCH or DELETE | `[{}, {SessionCookie, AntiforgeryToken}]`, because a request the cookie signs in is still checked |
  | GET or HEAD | `[{SessionCookie}]` |
  | POST, PUT, PATCH or DELETE | `[{SessionCookie, AntiforgeryToken}]` |
  | bearer route | one `{BearerToken: [name]}` alternative per scope or application role (OpenAPI 3.1 allows role names for non-OAuth schemes) |

  The document transformer then declares, with `OpenApiDocument.AddComponent`, exactly the schemes the operations name:
  - `SessionCookie`, an apiKey in the cookie `__Host-erp-session`;
  - `AntiforgeryToken`, an apiKey in the header `X-XSRF-TOKEN`, whose description names the 400 problems, the renewal route and the logout form field;
  - `BearerToken`, an http bearer scheme with format JWT, once a route takes bearer tokens.

  Every reference is built with the document being generated, so no requirement is written as `{}` by accident. `OpenApiSecurityTests` checks each operation, the declared schemes and a bearer route of a test host. The top-level `security` stays unset.
- **Tests sign their own tokens.** `TestTokenIssuer` (`Dewiride.Erp.Testing/Authentication/BearerTokens`) signs v2.0-shaped access tokens for `TestUsers` and `TestApplications` with one RSA key per test process (`ForPerson`, `ForApplication`, `Issue`, `IssueUnsigned`). `ErpApiFactory` gives every test host's bearer scheme a `StaticConfigurationManager` holding that key, so no test downloads metadata or keys, and with the exact issuer comparison no refused token reaches Entra either. `TestAuthHandler` falls through to `RouteSignInScheme` instead of the cookie.
- **Earlier decisions this changes.**
  - ADR-0004 and ADR-0031: a second scheme signs in, chosen per route, and the forbid title of the shared problems is neutral.
  - ADR-0012: building blocks declare platform flags in the catalog.
  - ADR-0019: a bearer caller is partitioned per actor too.
  - ADR-0033: the antiforgery check follows the route and the identity that signed the request in, not `AuthenticateAsync("Cookies")` alone.

## Consequences

- No product route takes bearer tokens yet. `integrations-public-api-and-webhooks` will:
  - give the registration an Application ID URI, its delegated scopes and its application-only app roles through `graph.sh`, with the `idtyp` optional claim and `include_user_token`;
  - protect its route group with `RequireBearerToken`;
  - switch the flag on per label.

  Until then the flag stays off and no client can obtain a token.
- A change of the flag reaches the API within `Erp:Platform:Configuration:RefreshInterval`, like any flag; turning it off refuses the next request of every client.
- A bearer caller has no session: `/api/auth/me`, `/api/auth/session` and every session route refuse it, and signing out does not reach it. Its token stays valid until it expires, 60 to 90 minutes by Entra's defaults.
- The web app reads `/api/auth/me` and `/api/auth/session` from `authentication-web-login-page-and-session-integration`; the generated client already has both.
- The `AntiforgeryToken` scheme cannot describe the logout form field, because an apiKey lives only in a query, a header or a cookie, so its description names the field.
- Reading the session never renews it, so a page that only watches its session lets an idle person be signed out after `SessionIdleTimeout`, as intended.
