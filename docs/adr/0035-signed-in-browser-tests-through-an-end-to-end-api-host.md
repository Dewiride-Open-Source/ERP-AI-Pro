# ADR-0035: Signed-in browser tests through an end-to-end API host

Status: accepted
Date: 2026-10-05

## Context

- Until now no Playwright spec could sign anyone in. The API hosts the suite started were the product API run with `dotnet run`: the first read the App Configuration store (`local-dev` label) on a developer machine, and the gated one and both hosts in `e2e.yml` ran store-less with placeholder tenant and client ids and a throwaway certificate (ADR-0031). Neither can complete a Microsoft sign-in, and a test cannot mint the session cookie of a process it does not run in, because the cookie is protected by that process's key ring (ADR-0032) and accepted only while the person's account is in its token cache.
- `authentication-web-login-page-and-session-integration` locks every page and the API together, after which every spec that opens a page needs a session. The signed-in proofs ADR-0033 left open (the upload, the download link and the delete carry `X-XSRF-TOKEN`; a change without it is refused) need one now.
- Playwright's WebKit 1.63 on Windows keeps a `Secure` cookie set over `http://localhost` but never sends it back or shows it to scripts (ADR-0033), and the session and antiforgery cookies are `Secure` by design. Relaxing the cookies for tests is refused: a signed-in WebKit run needs an https origin.
- The scope asked for an E2E-only login endpoint issuing the real auth cookie for seeded users, solely when `ASPNETCORE_ENVIRONMENT` is `Testing` and never in production images, and for the `TestAuthHandler` scheme to be available to it.

## Decision

- **An end-to-end API host, outside every image.** `backend/Tests/EndToEnd/Dewiride.Erp.Testing.EndToEndHost` is an executable whose `Program` calls `EndToEndHost.RunAsync` (`Dewiride.Erp.Testing.EndToEnd`). It runs the API a host test builds, `ErpApiFactory.ForEnvironment("Testing")`, on Kestrel through `WebApplicationFactory<Program>.UseKestrel(port)` and `StartServer()`. The factory listens on `http://127.0.0.1:<port>`. The host keeps everything `ErpApiFactory` adds:
  - the test identity provider and the test token endpoint, so nothing reaches Microsoft Entra ID;
  - the `Test` header scheme (`X-Test-User`), for checks at the level of the API;
  - an in-memory key ring;
  - a random attachments key and a per-process sign-in certificate.

  It also maps the persona sign-in. Arguments are `--port` and `--web-origin`, which becomes `Erp:Platform:Identity:WebOrigin`. The anonymous rate limit is 6000, the value of the `local-dev` label, because every browser project shares the loopback partition, as on a developer machine. The process sets its current directory to its output directory, because `WebApplicationFactory` reads the API's content root from `MvcTestingAppManifest.json` in the current directory.
- **Only in Testing.**
  - `EndToEndHostSettings.Read` refuses to start unless `ASPNETCORE_ENVIRONMENT` is `Testing` (compared without case, as `IsEnvironment` does).
  - `PersonaSignIn.Map` throws when its host runs in any other environment, so wiring it into a Development host fails at startup.
  - The project lives under `backend/Tests`, which `infra/docker/api.Dockerfile` never copies. The image publishes only `Hosts/Api`, `Hosts/HealthProbe` and `Hosts/Migrator`, and `LayerTests.ProductAssemblies_NeverReferenceTheTestingLibrary` keeps every product assembly off `Dewiride.Erp.Testing*`. No API image can contain the sign-in.
- **A world of its own.**
  - Database: `SqlTestDatabase.WithNamePrefix("ErpAiProE2E_", 10 minutes)` creates, migrates and seeds a database on the server named by `ERP_TEST_SQL_CONNECTION`.
  - Blob storage: `BlobTestContainer.WithNamePrefix("erpe2e-", 10 minutes)` creates a container on the Azurite named by `ERP_TEST_BLOB_EMULATOR_HOST`.
  - Presence and shutdown: while it lives, the fixture holds a session application lock named after its database, on a connection of its own without pooling, and a 60-second lease on its container, renewed every 20 seconds. A graceful stop drops both. Playwright kills its servers on Windows, so each start first removes the databases whose lock is free and the containers whose lease has run out, once they are older than ten minutes; a host that is still running, however long, keeps its own. The backend test fixtures do the same, after 24 hours.

  A run reaches neither Azure nor Entra and leaves the developer's `ErpAiPro` database and storage account alone, locally and in CI alike.
- **The persona sign-in.** `POST /api/__test/sign-in/{persona}` is anonymous and maps `accountant` and `administrator` to the personas of `TestUsers`. Each call signs in a new person of the persona:
  - The person has an object id from `Guid.CreateVersion7()` and the persona's name, user name and roles.
  - The person is admitted to `TestTokenEndpoint`, and MSAL redeems `TestTokenEndpoint.CodeFor(person)`, which puts the account in the token cache.
  - The cookie scheme then signs the person in, so `SessionCookieEvents.SignedIn` issues the antiforgery pair.
  - It answers `{ id, name, userName, roles }`, the shape of `GET /api/auth/me`. An unknown persona answers 404 and signs nobody in.

  New people instead of fixed ones, because a sign-out revokes every session of an account (ADR-0032), and two tests signing out one shared persona would sign each other out. The per-actor rate limit would also be shared.

  The route is under `/api`, a prefix the web app hands to the API. The request goes through the web origin, as a browser's Entra callback does, so the cookies land where the pages come from. It carries `DisableAntiforgery()`, like the callback it stands in for. The browser specs never use the header scheme, which would skip the cookie, the session check and the antiforgery check they exist to prove.
- **Every web origin is https.**
  - `frontend/e2e/servers/https-front.ts` is a Node program without dependencies. It ends TLS on 127.0.0.1 and ::1 with a certificate openssl makes for the run (`CN=localhost`, SAN `localhost`, `127.0.0.1`, `::1`) and forwards each listener to one `next start` server. Like the edge proxy, it adds `X-Forwarded-Proto: https`, `X-Forwarded-Host` and `X-Forwarded-For`, drops the hop-by-hop headers in both directions, keeps an idle browser connection for 60 seconds, and ends the browser's response when the web server's ends early.
  - Every server of the suite listens on loopback only: the API hosts on 127.0.0.1, `next start` with `-H 127.0.0.1`, and the front.
  - `next start` has no https of its own, and production ends TLS at the edge proxy, so a front mirrors production.
  - The origins: the API hosts listen on 5180 (primary) and 5181 (gated). `next start` listens on 3100, 3101 (no API) and 3102 (gated). The fronts listen on 3200, 3201 and 3202, the origins the specs open.
  - None of these ports is one the development loop uses, so `reuseExistingServer` can never pick up a `dotnet run` or `pnpm dev` that lacks the sign-in.
  - Playwright runs with `ignoreHTTPSErrors: true`, and the front's start check sets the same.
- **Downloads in WebKit on Windows.** Playwright's WebKit on Windows starts no download from a page whose certificate it was told to accept; the download request never reaches the network layer. A bare Node https server shows the same on Windows. Chromium and Firefox download there, and WebKit in the Playwright 1.63 Linux image downloads from the same kind of origin.

  So, for WebKit on Windows only, `AttachmentsPage.download` fetches the link the page opened. It records it with a capturing click listener and uses the browser context's cookies. The page still has to start the download of the right file under the right name. Everywhere else it waits for the browser's own download.

  Trusting the run's certificate would mean adding a root to the developer's Windows store, which is refused.
- **The Playwright fixture.**
  - The option `persona` (`"accountant"`, `"administrator"`, default none) makes the `context` fixture sign a new person in before the test's first page, through `context.request`, which shares its cookies with the pages.
  - `person` exposes who it is, and `requestToken(context)` reads the readable `__Host-erp-xsrf` cookie.
  - Every test that names a persona gets a session of its own; no storage state is shared.
- **CI.** `e2e.yml` builds the end-to-end host and starts both hosts with `ASPNETCORE_ENVIRONMENT=Testing`. It points `ERP_TEST_SQL_CONNECTION` at the server-level connection string of `start-sql-server` and `ERP_TEST_BLOB_EMULATOR_HOST` at Azurite, starts the three web servers and the front, and points the `E2E_*` variables at the https origins. The migrator step and the generated attachments key and sign-in certificate are gone, because the host makes its own.
- **Earlier decisions this changes.**
  - ADR-0031: the Playwright API hosts no longer run with placeholder ids and a generated certificate. They are test hosts with the test identity provider, so the sign-in redirect is checked in every run.
  - ADR-0022: local Playwright keeps attachments in Azurite, not in the development storage account.
  - ADR-0033: WebKit's Secure cookies are sent, because the origin is https; the signed-in proofs of the header are added.
  - ADR-0028: the suite's ports and origins change, and the same server set runs locally and in CI.

## Consequences

- The automatic suite no longer reads the App Configuration store, resolves Key Vault references or writes to the development storage account, and it never completes a real Microsoft sign-in. `docs/guides/local-development.md` (section "Manual sign-in check") describes the run that does: a real account, `/api/auth/me`, an upload and a download, and the sign-out.
- A local run needs `ERP_TEST_SQL_CONNECTION`, `ERP_TEST_BLOB_EMULATOR_HOST`, a running `erp-azurite` and openssl, as the backend tests do. A run killed on Windows leaves its database and container until a start ten minutes later removes them.
- When `authentication-web-login-page-and-session-integration` locks the pages, specs opt into `persona` or the fixture's default changes. The origin without an API cannot sign in, so its specs will need a session cookie that the page's optimistic check accepts.
- When `user-management-roles-and-permissions` brings users and permissions, the persona sign-in must create or seed the person's user record and permissions the same way.
