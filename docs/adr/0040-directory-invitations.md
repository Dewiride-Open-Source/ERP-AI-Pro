# ADR-0040: Directory invitations

Status: accepted
Date: 2026-10-10

## Context

- Since ADR-0039 an administrator registers a person before their first sign-in with `POST /api/identity/users`, by Entra object id or by work email, and the record carries whatever name and email the administrator typed until that sign-in. An object id had to be copied from the Entra admin center.
- The sign-in registration requires an app role assignment (`appRoleAssignmentRequired`), so nobody signs in until `Erp.User` or `Erp.Admin` is assigned to them in Entra. The ERP has no roles of its own until `user-management-roles-and-permissions`.
- The scope of `user-management-directory-invitation-via-microsoft-graph` asks for a search of the directory through Microsoft Graph, invitees pre-registered with an intended role, matched by object id or email at their first sign-in, and integration tests with a fake Graph client.
- Microsoft Graph (List users, Get user, the `$search` query parameter and the advanced queries on directory objects, read 2026-10-10):
  - `GET /users` takes the delegated permission `User.ReadBasic.All` as its least privileged one, which reads only `displayName`, `givenName`, `id`, `mail`, `photo`, `securityIdentifier`, `surname` and `userPrincipalName`.
  - `$search` on directory objects is an advanced query that needs the header `ConsistencyLevel: eventual`. It matches tokens of `displayName` and the start of other properties.
  - A clause is written `"property:text"`, with `"` and `\` escaped by a backslash; an `&` fails even when it is encoded.
  - Assigning an app role through Graph (`POST /servicePrincipals/{id}/appRoleAssignedTo`) needs `AppRoleAssignment.ReadWrite.All` and `Application.Read.All`. Delegated, the person must also hold an Entra directory role such as User Administrator or Application Administrator.
- Owner decisions of 2026-10-10:
  - An invitation does not grant access: access is still given by assigning an app role in Entra. The ERP assigning roles was rejected, because the permission it needs can grant the roles of any application in the tenant, Microsoft Graph's included. Turning off the assignment requirement was rejected too.
  - The role of an invitation arrives with `user-management-roles-and-permissions`.
  - `User.ReadBasic.All` is added, with consent for the whole organisation, to both sign-in registrations, behind a dry run.

## Decision

- **The directory contract.** `BuildingBlocks.Application/Actors` declares:
  - `IPeopleDirectory`, with `SearchAsync(text)` and `FindAsync(objectId)`;
  - `DirectoryPerson(ObjectId, DisplayName, SignInName, UserPrincipalName, Mail)`;
  - `DirectorySearch(People, HasMore)`, with `MinTextLength` 2, `MaxTextLength` 100 and `MaxPeople` 25;
  - `DirectoryErrors`: `directory.person-not-found` (404), `directory.access-denied` (403) and `directory.unavailable` (503).
- **Delegated, on the administrator's behalf.** `BuildingBlocks.Authentication` implements the contract as `GraphPeopleDirectory`.
  - **Client.** A typed `HttpClient` named `MicrosoftGraph.HttpClientName` (`Erp.MicrosoftGraph`, base `https://graph.microsoft.com/v1.0/`), with the standard resilience handler every client carries (ADR-0020).
  - **Token.** `IAuthorizationHeaderProvider.CreateAuthorizationHeaderForUserAsync` issues it for `https://graph.microsoft.com/User.ReadBasic.All` (`MicrosoftGraph.ReadBasicProfilesScope`), for the request's user. It names the options of the sign-in scheme (`AuthenticationOptionsName` `OpenIdConnect`), because the default authenticate scheme is the route sign-in policy scheme.
  - **How the token is redeemed.** MSAL redeems the refresh token of the person's session from the token cache (ADR-0032). The sign-in itself still asks for no scope beyond signing in.
  - **The token cache stays bounded by the session.** The redemption writes the person's entry again, and Microsoft.Identity.Web's adapter would give that write a new absolute expiry, `SessionLifetime` plus the margin counted from the write. `SessionBoundTokenCacheStore`, between the adapter and `ProtectedTokenCacheStore`, gives a write made during a request of the person's own session the end of that session instead: their sign-in time plus `SessionLifetime` plus the margin. So every entry still expires at most `SessionLifetime` after its sign-in, plus the margin.
  - **Why delegated.** An application permission would read the directory at any time, with no administrator present. Microsoft recommends a delegated permission while a person is signed in, and an administrator then sees only what Entra lets them read.
- **Search and lookup.**
  - **The search request.** The search sends `GET users?$search="displayName:<text>" OR "mail:<text>" OR "userPrincipalName:<text>"&$select=id,displayName,mail,userPrincipalName&$orderby=displayName&$top=25&$count=true` with `ConsistencyLevel: eventual`. The text is trimmed, its `\` and `"` are escaped and the value is URL-encoded.
  - **The search answer.** `HasMore` says whether Graph returned `@odata.nextLink`. A user without an object id or a user principal name is left out. A blank display name is replaced by the user principal name, and a blank mail is null.
  - **The sign-in name.** A member signs in with their user principal name. A guest of another organisation is held under a user principal name that quotes, before `#EXT#`, the address it was invited with (Microsoft Entra External ID, "Properties of a B2B guest user"), and signs in with that address, which Graph reports as its mail. So `SignInName` is the mail of a user whose user principal name holds `#EXT#@` and has a mail, and the user principal name otherwise.
  - **The lookup.** It sends `GET users/{id}?$select=…`: a 404 is `directory.person-not-found`, and an answer naming another object id throws.
- **Failures.**
  - **Entra refuses the token.** `MicrosoftIdentityWebChallengeUserException` or `MsalUiRequiredException` (consent missing, an interaction required, no account in the cache) is `directory.access-denied`, logged at `Warning` with the MSAL error code and classification.
  - **Entra cannot issue the token.** Any other `MsalException` is `directory.unavailable`.
  - **Graph refuses.** A 401 or 403 is `directory.access-denied`.
  - **Graph is down.** These are `directory.unavailable`: a 408, 429 or 5xx once the resilience handler gives up, `HttpRequestException`, Polly's `ExecutionRejectedException` (a timeout, an open circuit, the rate limiter) and the client's own timeout.
  - **Graph does not answer.** The request's own timeout (`Erp:Platform:Host:RequestTimeout`, 30 seconds by default, ADR-0018) starts before the resilience handler's total timeout of 30 seconds, so a directory that never answers cancels the request first: it answers 504 `request.timeout`, like any request that runs too long.
  - **Any other answer** throws, naming the status and Graph's error code (500).
  - **Logs.** They carry the operation, the status and Graph's error code. They never carry the search text, Graph's message, a name or a token.
  - **Query redaction.** Since .NET 9 the runtime shows the whole query as `*` in the HTTP client factory's logs, its events and the `url.full` of the `HttpClient` span it creates ([observability](../guides/observability.md)). The server span records `url.query` with every value `Redacted`. `TelemetryTests` serves the directory over a real TLS connection on a loopback port (`DirectoryListener`), so the runtime records the Graph client span. It proves that span's `url.full` ends with `?*`, and that neither the text nor the token reaches any span or log record of the request, the Graph client's log lines included.
- **`ErrorKind.Unavailable`.** `ResultExtensions.ToProblem` answers it with 503 and the error's code (`Error.Unavailable`), for a dependency that cannot answer now.
- **Endpoints read the directory; handlers never do.** The token belongs to the request. The migrator and the test database host compose the module without the sign-in services, and the migrator validates its services when it builds in Development. So `InvitationEndpoints` reads the directory and hands the people it returned to `FindRegisteredPeopleQuery` and `InvitePersonCommand`.
- **The routes** under `/api/identity/users`, for holders of `Erp.Admin`:
  - **`GET /directory?search=`** takes `SearchDirectoryRequest`: required, at most 100 characters, at least 2 besides spaces at either end, without `&` or control characters, else 400 keyed `search`.
    - It answers 200 `DirectorySearchResponse { people: [{ entraObjectId, displayName, signInName, userPrincipalName, mail, personId }], hasMore }`.
    - `personId` names the record the person's sign-in would be admitted with (`FindRegisteredPeopleHandler`, the rule of `AdmitPersonHandler`): the record linked to the object id, else a record linked to no Entra account whose work email is the sign-in name.
  - **`POST /invitations`** takes an `Idempotency-Key` and `InvitePersonRequest { entraObjectId, employeeCode?, phoneNumber?, designation?, dateOfJoining? }`; the object id is required and never the empty GUID.
    - It reads the person from the directory and creates the record with `User.Invite`. The record is linked from the start, and its name and work email are the directory's display name and the normalised sign-in name. The answer is 201 with `Location` and the person.
    - A person the directory does not hold answers 404 `directory.person-not-found`.
    - Both routes answer 403 `directory.access-denied` and 503 `directory.unavailable` as above, and 504 `request.timeout` when the directory never answers.
    - It answers 409 `user.entra-object-id-taken`, `user.employee-code-taken`, or `user.work-email-taken` when a record linked to no Entra account holds the sign-in name. Only such a record holds an email a sign-in still matches; a linked record of another account kept an address Entra has since given this person, as the sign-in also allows.
  - **`POST /` registers by work email only.** `RegisterPersonRequest`, `RegisterPersonCommand` and `User.Register` have no object id. The invitation is the only way to name an Entra account before a first sign-in, so every such object id is one the directory holds, and the name and email are the directory's.
- **Entra.** `entra.sh` requests and grants, for all principals, `User.ReadBasic.All` on both sign-in registrations (`GRAPH_SCOPE_VALUES` in `scripts/azure/lib/graph.sh`), and `verify.sh --entra` checks it. Both changes were applied on 2026-10-10, after a dry run that showed exactly them.
- **No role on an invitation.** Access is granted by assigning `Erp.User` or `Erp.Admin` in Entra, as before.
- **Tests.**
  - **`TestDirectory`** (`Dewiride.Erp.Testing/Graph`) is the primary handler of the Graph client in every `ErpApiFactory` host. It answers the search and the lookup for the people a test adds. It answers only a bearer token `TestTokenEndpoint` issued for the directory scope, answers a search only with `ConsistencyLevel: eventual`, can refuse every request, and fails every other request.
  - **`TestTokenEndpoint`** redeems the refresh tokens it issued for the scopes MSAL asks. `WithholdConsent` makes it refuse them with `AADSTS65001`. So the integration tests run the real token acquisition through the token cache; each test signs in an administrator of its own.

## Consequences

- An administrator's search or invitation costs a Graph call. It also costs a token request whenever the cached access token lacks the directory scope or has expired, at most once an hour per administrator. The token cache holds one more access token per administrator.
- The consent covers the whole organisation, so any session's refresh token could be redeemed for `User.ReadBasic.All`. Only the people routes, for `Erp.Admin`, read the directory. Whoever takes over the API process can read the basic profiles of the directory while sessions last (residual risk 12 of `docs/security/auth-threat-model.md`).
- `User.ReadBasic.All` cannot read `userType` or `accountEnabled`, so guests and disabled accounts appear in the search; a disabled account cannot sign in.
- An invited person signs in only once an app role is assigned to them in Entra; until then their record shows no sign-in.
- A directory search racing its person's sign-out can write the token cache entry back after the sign-out removed it. The sign-out record still refuses every session of that person, and the entry expires 35 minutes after that write at most (residual risk 13 of the threat model).
- Earlier decisions this changes:
  - ADR-0016 and ADR-0017: the error kinds gain `Unavailable` (503).
  - ADR-0031: the sign-in registrations request `User.ReadBasic.All` besides the sign-in scopes; the sign-in itself asks for none of it.
  - ADR-0032: the token cache is written during a session too, and `SessionBoundTokenCacheStore` keeps such a write within the session.
  - ADR-0035: `TestTokenEndpoint` redeems refresh tokens, and every test host answers Graph through `TestDirectory`.
  - ADR-0039: an administrator registers by work email, and names an Entra account only through an invitation.
