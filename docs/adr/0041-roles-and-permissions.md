# ADR-0041: Roles and permissions

Status: accepted
Date: 2026-10-11

## Context

- **What existed.**
  - Since ADR-0031, the fallback policy requires a signed-in caller on every route, and nothing finer.
  - ADR-0039 put every people route behind the app role `Erp.Admin`. Module descriptors already declared permission names (`ModuleDescriptor.Permissions`, checked against the module's prefix), but nothing enforced them.
  - The web app showed every navigation entry to every signed-in person (ADR-0037).
- **The roadmap scope** asked for:
  - a permission catalogue generated from module declarations;
  - `Role` and `RolePermission` entities with seeded system roles (Owner, Admin, Finance, HR, Manager, Employee) mapped from Entra app roles;
  - a permission authorization handler and a `RequirePermission` endpoint extension;
  - the permissions returned by `GET /api/auth/me`;
  - an architecture test that every non-anonymous endpoint declares a permission.
- **How app roles reach the ERP.** The registration's app roles (`Erp.User`, `Erp.Admin`, `scripts/azure/entra/app-roles.json`) reach the ERP as the `roles` claim:
  - of the ID token at sign-in, which the session keeps until it ends (12 hours at most, ADR-0031);
  - of a person's own access token (ADR-0034).

  The admission already reads the person's record on every request (ADR-0039).
- **Owner decisions** (2026-10-10 and 2026-10-11):
  - The roles are the ERP's own, assigned in the ERP, except Administrator. The holders of `Erp.Admin` in Entra are exactly the administrators, and nothing in the ERP makes anyone one.
  - There is no Owner role.
  - The roles are built in: what each role allows is code. There are no custom roles and no editable table of what a role allows.
  - Every employee can upload and open files. Deleting files and reading the system information are an administrator's.
  - An invitation can name the person's roles.

## Decision

- **The roles.** `ErpRole` (`BuildingBlocks.Kernel/Access`) is `Employee`, `Finance`, `Hr`, `Manager` and `Administrator`. It lives in the kernel because the users domain stores roles and module descriptors name them.
  - A person holds:
    - Administrator while their token carries `Erp.Admin`;
    - Employee while it carries `Erp.User`;
    - the Finance, Hr and Manager roles assigned to their record in the ERP.
  - The users module's Contracts type `PersonRole` (`finance`, `hr`, `manager` on the wire) is the assignable subset, so the API cannot even read a request that names Administrator or Employee (400 `request.malformed`).
- **The declarations.** `ModuleDescriptor.Permissions` is a collection of `PermissionDeclaration(Name, HeldBy)`: the permission and the built-in roles that hold it.
  - `PermissionCatalog` is a singleton built by `AddModules`. It refuses three things when the host starts:
    - a name that is not four lower-case segments `<domain>.<module>.<feature>.<action>`;
    - `Administrator` in `HeldBy`, because Administrator holds every permission;
    - a name declared twice.
  - It answers `Names` (ordinal order), `Contains` and `PermissionsOf(roles)`.
  - No table holds roles or what they allow, and no seeder writes any, because the owner chose built-in roles only. A permission is an administrator's alone until its module names other roles.
- **The catalogue today:**

  | Permission | Held by | Required by |
  |---|---|---|
  | `identity.users.people.read` | Administrator | `GET /api/identity/users`, `GET /{id}` |
  | `identity.users.people.register` | Administrator | `POST /`, `GET /directory`, `POST /invitations` |
  | `identity.users.people.update` | Administrator | `PUT /{id}` |
  | `identity.users.people.change-status` | Administrator | `PUT /{id}/status` |
  | `identity.users.people.delete` | Administrator | `DELETE /{id}` |
  | `identity.users.people.assign-roles` | Administrator | `PUT /{id}/roles`, and naming roles in `POST /` or `POST /invitations` |
  | `platform.attachments.files.read` | Employee | `GET /api/platform/attachments`, `GET /policy`, `GET /{id}`, `POST /{id}/download-links`, `GET /{id}/content` |
  | `platform.attachments.files.upload` | Employee | `POST /api/platform/attachments` |
  | `platform.attachments.files.delete` | Administrator | `DELETE /api/platform/attachments/{id}` |
  | `platform.system-info.info.read` | Administrator | `GET /api/platform/system-info` |
  | `platform.system-info.startups.read` | Administrator | `GET /api/platform/system-info/startups` |

  Redeeming a download link needs `read` as well as being the link's actor, so a person whose permission was taken cannot redeem a link they made earlier.
- **A caller's roles.** `CallerRoles.Of` is in `BuildingBlocks.Authentication/Permissions`.
  - **App roles.** For a signed-in person, it maps the `roles` claims: `Erp.Admin` to Administrator and `Erp.User` to Employee.
  - **Assigned roles.** It adds the roles assigned to the person's record. The admission read supplies them: `IPersonAdmission.AdmissionOfAsync(objectId)` answers `PersonAdmission(AssignedRoles)`, or null when the record does not admit the person (`GetPersonAdmissionQuery`, the one indexed read ADR-0039 already makes).
  - **Where they are kept.**
    - The session check (`SessionCookieEvents.ValidatePrincipal`) keeps them in the request feature `PersonRolesFeature`, only once every check has accepted the cookie.
    - So does the bearer handler (`BearerTokenEvents.TokenValidated`), once it accepts a person's token.
    - The feature counts only while its object id is the caller's.
  - **Applications.** An application's token holds no role. Its roles are application roles, and an `Erp.Admin` or `Erp.User` among them counts for nothing (ADR-0034). So a permission route refuses every application until `integrations-public-api-and-webhooks` maps application roles to permissions.
  - **Test header.** A request signed in by the test header runs no admission, so it holds its app roles only.
- **Enforcement.**
  - **`RequirePermission(permission)`** (`BuildingBlocks.Endpoints/Authorization`, so a module needs no reference to the sign-in building block) adds three things:
    - a policy of `RequireAuthenticatedUser` and `PermissionRequirement`, because a route's own policy replaces the fallback policy;
    - `PermissionMetadata`;
    - `ProducesProblem(403)`.

    A route requires one permission: a second call on the route or its group fails when the route is built, because every policy applies and the caller would need both.
  - **`PermissionAuthorizationHandler`** succeeds when the caller's roles hold the permission. A permission no module declares would refuse everyone, administrators included, so the handler throws for it (500).
  - **The refusal is the route scheme's forbid.**
    - **Session cookie:** 403 `request.forbidden` (ADR-0031).
    - **Bearer token whose scopes or application roles grant the route:** 403 with `WWW-Authenticate: Bearer` and no error, since no scope would cure it, recorded as `BearerTokenRefused` with the reason `missing-permission`.
    - **Bearer token that the route's access refuses as well:** `insufficient_scope`, as before.
  - **Pipeline order.** Authorization runs before antiforgery and the feature gate (ADR-0018). A caller without the permission is answered 403 before a missing antiforgery token (400) or a disabled module (404 `feature.disabled`) is reported.
- **Routes for any signed-in caller.** These need no permission: `GET /api/auth/me`, `GET` and `POST /api/auth/session`, `GET /api/auth/antiforgery` and `GET /api/platform/features`. The architecture tests (`ModuleCatalogTests`) fail on four things:
  - a route that is neither anonymous, on that list (`SignedInRoutes`), nor permission-bound;
  - a stale entry on the list;
  - a permission route that also allows anonymous callers, which would skip authorization altogether;
  - a required permission the catalogue lacks.
- **Checks that depend on the request.** `ICallerPermissions.Holds(permission)` is scoped (`CallerPermissions`, the caller's permissions worked out once per request). It is for a check the route cannot make up front. Today that is naming roles in a registration or an invitation: without `identity.users.people.assign-roles`, it answers 403 `request.forbidden` and stores nothing.
- **The record's roles.**
  - **Storage.** `User.Roles` is an EF Core primitive collection of member names in the SQL Server `json` column `identity_users.Users.Roles`, which defaults to `[]` (migration `AddPersonRoles`). The admission reads it within the same row.
  - **The rule.** `User.AssignRoles` keeps Finance, Hr and Manager, distinct and ordered, and refuses any other role with `user.role-not-assignable`.
  - **`PUT /api/identity/users/{id}/roles`** takes `AssignPersonRolesRequest { roles (required, at most 3), version }` and answers the record with its new `version`.
    - Roles the record already holds leave its version as it was.
    - A stale version answers 409 `concurrency.conflict`.
    - An administrator may assign roles to their own record; Entra makes them Administrator whatever it holds.
  - **Registration and invitation.** `RegisterPersonRequest` and `InvitePersonRequest` take optional `roles`, and `PersonResponse.roles` lists the assigned roles only.
- **What the web app learns.** `GET /api/auth/me` answers `permissions`: the caller's permissions in ordinal order. The OpenAPI document lists every catalogue name in the enum schema `Permission` (`PermissionSchemaDocumentTransformer`), which `CurrentUserResponse.permissions` refers to. So the generated client types a permission, and a name no module declares fails the web app's type check.
- **What the web app shows.**
  - **The person's permissions.** `readSession` carries them (`SignedInPerson.permissions`). `holdsPermission(person, permission)` and `mayShow(session, permission)` (`shared/auth/permissions.ts`) decide what renders.
  - **Navigation.** Every `nav.ts` names the `permission` its entry needs: `platform.attachments.files.read` for Attachments and `platform.system-info.info.read` for System. `visibleEntries` (`shared/layout/navigation/visible-entries.ts`) keeps an entry whose flag is on and whose permission the person holds. `AppShell` uses it for the sidebar, the command palette and the breadcrumbs, and `StartPage` for its cards.
  - **Pages.** A page wraps what it shows in `RequirePermission` (`shared/auth/require-permission.tsx`). For a person without the permission it renders `NoAccessMessage`: "You do not have access to this page", "Ask an administrator for access." and a link home. The page decides rather than its layout, because a layout cannot keep the page below it from rendering or from fetching.
  - **Controls.** The attachments page shows the upload card only to holders of `upload`, and the Delete action only to holders of `delete`.
  - **Server Functions.** Every product Server Function starts with `const person = await requireSignedInPerson();` and then `if (!holdsPermission(person, <permission>)) return <refusal>;`, which `pnpm lint` enforces (rule X4). The refusal reads "You do not have permission to do this." (`formMessages.forbidden`). The design harness's Server Functions, which change demo data only, keep `await requireSignedInPerson()` alone.
  - **An unreadable session.** When the session cannot be read (`unavailable`), the web app shows every entry and control whose flag is on, as it does for flags it cannot read. Each call the person then makes is still refused by the API.

## Consequences

- **When a change counts.**
  - A role assigned or taken in the ERP counts from the person's next request.
  - An app role assigned or removed in Entra counts at their next sign-in, because the session keeps the ID token's `roles`. Until then an open session keeps the app roles of its sign-in, Administrator included, as it did before this item. Deactivating the record refuses those sessions only while it stays deactivated (residual risk 10 of `docs/security/auth-threat-model.md`).
- **No extra read per request.** The admission read ADR-0039 added now carries the assigned roles.
- **Refusals.** An application token reaches no permission route until application roles map to permissions. A person without the permission is answered 403 on a disabled module's route instead of 404.
- **Pages a person may not open** answer 200 with the no-access message. Next.js's `forbidden()` is experimental, and ADR-0024 refuses experimental special files.
- **Stale navigation.** The shell's navigation reflects the person's permissions as of the last time the server rendered it: a full load, or a refresh after a change. A page itself shows the no-access message from the next navigation.
- **Personas.** The end-to-end personas stay `accountant` (Employee) and `administrator`. No persona without permissions joins them, because the registration requires an app role, so nobody without one signs in. The accountant takes the refusal paths.
- **Earlier decisions this changes:**
  - ADR-0012: a disabled module's route answers 404 `feature.disabled` only to a caller who holds its permission.
  - ADR-0018: a missing permission (403) is answered before antiforgery and the feature gate.
  - ADR-0022: every attachment route requires its file permission, and redeeming a download link requires `read`.
  - ADR-0031: authorization is the fallback policy plus a permission per route.
  - ADR-0034: a permission route refuses an application's token, and a person's token whose holder lacks the permission is answered with a plain `Bearer` challenge and recorded as `missing-permission`.
  - ADR-0035: no persona without permissions; the accountant is the employee whom routes refuse.
  - ADR-0036: a product Server Function checks the permission right after `requireSignedInPerson()` (rule X4).
  - ADR-0037: navigation entries carry a permission, and the shell and the start page list only the entries the person may open.
  - ADR-0039: the people routes require the permissions above in place of the app role `Erp.Admin`; the record stores its assigned roles; the admission returns them.
  - ADR-0040: the directory search and the invitation require `identity.users.people.register`, and an invitation can name roles.
  - The roadmap scope: no `Role` or `RolePermission` entities and no Owner role (owner decisions above).
