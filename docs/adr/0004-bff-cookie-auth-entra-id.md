# ADR-0004: Backend-for-frontend cookie authentication with Microsoft Entra ID

Status: accepted
Date: 2026-09-19

## Context

Sign-in is Microsoft Entra ID only (single workforce tenant). ASP.NET Core guidance states that OpenID Connect public clients are no longer recommended for web apps and that access tokens must not be stored in the browser; the backend-for-frontend (BFF) pattern keeps tokens on a trusted server and shares only an HttpOnly cookie. Microsoft's official Next.js sample uses MSAL in the browser, which contradicts that guidance. Microsoft.Identity.Web supports the OpenID Connect cookie scheme and a JWT bearer scheme in the same application.

## Decision

- The .NET API host is the confidential OpenID Connect client: `AddMicrosoftIdentityWebApp` (authorization code + PKCE, single tenant) with callbacks under `/api/auth/` (`signin-oidc`, `signout-callback-oidc`) and explicit endpoints `/api/auth/login?returnUrl=`, `/api/auth/logout`, `/api/auth/me`, `/api/auth/antiforgery`.
- The browser holds only the HttpOnly, Secure, SameSite=Lax authentication cookie. The web app and the API are served from one origin (Next.js rewrites locally, the edge proxy in production), so there is no CORS and no token in JavaScript.
- Unauthenticated `/api/*` requests receive 401 ProblemDetails, never a redirect; `returnUrl` must be a local path.
- Antiforgery uses the `X-XSRF-TOKEN` header with a readable token cookie on every state-changing request.
- Token acquisition for downstream APIs (Microsoft Graph) is enabled only when a downstream call exists, with a distributed token cache backed by SQL Server in production.
- A JWT bearer scheme is added only when a non-browser client needs it, with explicit schemes on those endpoints.
- MSAL is never used in the browser.

## Consequences

- Authentication has one implementation, in the API; the web app only redirects and forwards cookies.
- Server Components can call the API with the incoming cookie, so pages render authenticated data server-side.
- The end-to-end sign-in strategy for Playwright (real test user vs a Testing-environment-only endpoint) is decided in the authentication phase by its own ADR.
