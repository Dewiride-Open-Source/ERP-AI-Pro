# ADR-0047: User lifecycle: the account check, the team handover and two administrators at once

Status: accepted
Date: 2026-10-18

## Context

- **What existed.**
  - Deactivation, reactivation and soft deletion of a person's record (`PUT /api/identity/users/{id}/status`, `DELETE /api/identity/users/{id}`), and the per-request admission check that refuses every session and person's bearer token of a deactivated or deleted record (ADR-0039), so the roadmap scope's server-side security stamp was already answered.
  - Departments, designations and positions (ADR-0042): a deactivated person keeps the positions that name them as manager and leaves the organisation chart, and a person someone reports to today or later cannot be deleted (409 `user.manages-people`).
  - The directory read on behalf of the signed-in administrator with the delegated `User.ReadBasic.All` (ADR-0040), which cannot read `accountEnabled`.
- **Residual risks of `docs/security/auth-threat-model.md`.**
  - 10: an account disabled or deleted in Entra keeps its open ERP sessions until they end (30 minutes idle, 12 hours at most), and its record stays active.
  - 11: administrators can deactivate each other, so two of them doing it at once leave no active administrator.
- **Microsoft Graph** (Microsoft Learn, "List users", "user: delta", "Microsoft Graph permissions reference", "Known issues: query parameters").
  - Reading `accountEnabled` without a signed-in user needs the application permission `User.Read.All` ("Read all users' full profiles", admin consent); the application `User.ReadBasic.All` covers the basic profile only (`displayName`, `givenName`, `id`, `mail`, `photo`, `securityIdentifier`, `surname`, `userPrincipalName`).
  - `directoryObjects/getByIds` needs `Directory.Read.All`.
  - `$filter` with `in` is limited to 15 expressions.
- **The scheduler and the outbox come later.** `business-building-blocks-background-jobs-and-scheduling` names the nightly directory sync as its first recurring job, and integration events are delivered only once `business-building-blocks-transactional-outbox-and-integration-events` exists.
- **Owner decisions of 2026-10-18.**
  - The ERP checks Microsoft Entra for people whose account was disabled or deleted, with the new application permission. The check runs nightly once it is scheduled; until then an administrator runs it from the People page. The permission is added only after a dry run shows it is the only change in Entra. The owner accepted that whoever takes over the API's credential could then read the directory at any time.
  - A person the check finds disabled or deleted is deactivated at once; the audit trail shows why, and an administrator can reactivate them.
  - Deactivating a person who manages anyone asks who their team reports to from that day. When the check deactivates a manager, the People page marks the team as needing a new manager.

## Decision

- **Two administrators at once.** The acting administrator holds `Erp.Admin`, so deactivating or deleting someone else never removes the last administrator by itself; only two administrators removing each other concurrently can. Every status change and deletion of a person therefore takes the transaction lock `identity_users.organisation` (`OrganisationLock`) and then reads the records naming the acting person (`ActingPerson`): when none of them is active and undeleted, the change is refused with 409 `user.actor-inactive`. Of two administrators who deactivate each other at once, the second to take the lock finds its own record deactivated, so one of them stays active. An administrator still cannot change the status of or delete their own record (`user.own-status`, `user.own-deletion`). A request whose actor has no record (the system actor) is not checked.
- **The reason a record was deactivated.** `User.DeactivatedBecause` (`DeactivationReason`: `Administrator`, `AccountDisabled`, `AccountDeleted`; null while active) is set by every deactivation and cleared by a reactivation; deactivating a record already deactivated keeps its first reason. It is recorded in the change records, shown on the person page and answered as `deactivatedBecause` (`PersonDeactivationReason`: `administrator`, `accountDisabled`, `accountDeleted`). The migration `AddDeactivationReason` sets `administrator` on the records already deactivated.
- **The team handover.**
  - A person's team is everyone with a position that names them as manager and holds today or later (India Standard Time).
  - Deactivating a person whose team is not empty needs `team` in the request: the new manager's id, or null for no manager. The new manager must exist, be active, not be that person and not be in the team (422 `user.team-manager-not-found`, `user.team-manager-inactive`, `user.team-manager-same-person`, `user.team-manager-in-team`). Without a handover the request is refused with 422 `user.team-handover-required`. `GET /api/identity/users/{id}/team` (`identity.users.positions.read`) lists the team, by the same rule (`TeamPositions`), for the dialog that asks.
  - For each member of the team, the position in effect today is followed by a new position from today with the same department and designation and the new manager; a position that starts today, and each later position that names the person, is withdrawn and recorded again on its own date with the new manager. The reporting-cycle rule holds over the result (422 `user.team-reporting-cycle`).
  - Deactivating a record that is already deactivated changes nothing on it, but the team rule still applies, so the same request hands over the team of a person the account check deactivated.
  - The positions and the deactivation commit in one transaction under the organisation lock, so a refused handover changes nothing.
  - A request that sends `team` also needs `identity.users.positions.record` and `identity.users.positions.withdraw`.
- **The account check.**
  - `IDirectoryAccounts` (`BuildingBlocks.Application/Actors/PeopleDirectory`) answers the state of given Entra object ids: enabled, disabled or deleted.
  - Its Graph implementation (`BuildingBlocks.Authentication/Graph`) reads with an application token for `https://graph.microsoft.com/.default`, made by `IAuthorizationHeaderProvider.CreateAuthorizationHeaderForAppAsync` from the sign-in registration's certificate. It asks for the ids 15 at a time, `GET users?$filter=id in (…)&$select=id,accountEnabled`, and an id missing from its answer is deleted.
  - The users module reads the active, undeleted records linked to an object id, asks for their states and deactivates each disabled or deleted one with that reason. When the answer lists none of them as enabled, nothing changes and the check answers 503 `directory.unavailable`, so an empty or wrong answer never deactivates everyone.
  - The check is `POST /api/identity/users/directory/account-check` (`identity.users.people.change-status`), answering how many records were checked and who was deactivated. Running it again changes nothing more, so it takes no idempotency key.
  - The orchestration takes the directory as an argument, because the migrator composes the module without the sign-in services. `business-building-blocks-background-jobs-and-scheduling` runs the same code nightly as the system actor, under which the audit trail names the ERP.
- **Nullable references in the document.** .NET describes a nullable property whose type is a record or an enum (`team`, `deactivatedBecause`) as `oneOf` a `null` schema and the reference, and lists `null` among the enum's values, which the client generator cannot express. `NullableReferenceTransformer` (`BuildingBlocks.Endpoints/OpenApi`) describes such a property by the reference alone and drops `null` from enum values; the generated client types every property as possibly null anyway, and the descriptions say when it is null ([HTTP conventions](../architecture/http-conventions.md)).
- **The marked team.** `PersonResponse` carries `managerActive`, so the People list and the person page mark a person whose manager of today is deactivated as needing a new manager.
- **The permission in Entra.** `entra.sh` adds `User.Read.All` as an application permission (`requiredResourceAccess` type `Role`) to both sign-in registrations, and grants it with an app role assignment of the registration's service principal to Microsoft Graph's; `verify.sh --entra` checks both. Granting a Microsoft Graph application permission needs a Privileged Role Administrator or a Global Administrator.
- **The offboarding hook moves.** The scope's offboarding checklist hook for `hr-core` needs integration events in both directions: a deactivation for the checklist, and the exit process revoking access. It is built by `hr-core-onboarding-and-offboarding` once the outbox delivers integration events, with the handover rules above.

## Options rejected

- **A column for who held `Erp.Admin` at their last sign-in, and a count of the other active administrators.** The acting administrator always holds the role, so the count only matters when two administrators remove each other at once, which the lock and the actor's own record settle. The column would also read false for every administrator until their next sign-in.
- **Listing every account in the directory.** An account missing from the list reads as deleted, so in tests, and on the shared end-to-end API host, a check would deactivate every record whose account the test directory does not know. Asking for the linked ids reads only what the check decides about.
- **`directoryObjects/getByIds`:** it needs `Directory.Read.All`, broader than `User.Read.All`.
- **The delta query:** it needs the same permission, plus a stored delta link and a first full read, for a check of a few hundred records.
- **The delegated `User.Read.All` of the administrator who presses the button:** the nightly check runs without a person.
- **Marking leavers without deactivating them:** the owner chose deactivation, which ends their sessions at their next request.
- **A handover that keeps the team under the deactivated manager:** the organisation chart and every later approval would name someone who has left.
- **The check as the system actor when an administrator runs it:** the administrator asked for it, so the audit trail names them, and the recorded reason says why each record was deactivated.

## Consequences

- An account disabled or deleted in Entra is deactivated in the ERP the next time the check runs: at once when an administrator presses the button, nightly once the check is scheduled. Its sessions end at their next request.
- Whoever takes over the API's certificate can read the full profiles of the whole directory at any time, not only while sessions are open (residual risk 12). Every member of the tenant can read most of the same profiles in Entra by default.
- A team never reports to a deactivated manager after an administrator's deactivation; after the check's, it does until an administrator records a new manager, and the People page marks it.
- Two administrators cannot leave the ERP without an active administrator by removing each other.
- `hr-core` builds the offboarding hook on the outbox.
