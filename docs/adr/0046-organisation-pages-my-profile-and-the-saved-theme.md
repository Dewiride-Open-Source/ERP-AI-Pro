# ADR-0046: Organisation pages, My profile and the saved theme

Status: accepted
Date: 2026-10-18

## Context

- **What existed.**
  - The users module answered the directory search and the invitation (ADR-0040), departments, designations, positions and the organisation chart (ADR-0042), and the people pages and the permission catalogue had their pages (ADR-0045). No page searched the directory, kept departments or designations, or drew the chart.
  - A person could read their own record only through `GET /api/identity/users/{id}`, which needs `identity.users.people.read`, held by administrators alone.
  - `next-themes` kept the theme in each browser's `localStorage` (`theme`), set by the top bar's `ThemeToggle` of `packages/ui`.
  - The account button showed the person's initials.
- **The roadmap scope:** the invite dialog with directory search; department and designation admin pages with an org-tree preview; my-profile with the Graph photo and the theme preference; Playwright on desktop and phones with screenshots.
- **Microsoft Graph** ("Get profilePhoto", Microsoft Learn): `GET /me/photo/$value` answers the largest photo and `GET /me/photos/{size}/$value` a fixed size (48x48 to 648x648 for Microsoft 365; photos kept in Microsoft Entra ID can be any size), 404 when there is none. For a work or school account the least privileged delegated permission is `ProfilePhoto.Read.All`, and `User.Read` and `User.ReadBasic.All` also allow it; both sign-in registrations already hold `User.Read` and `User.ReadBasic.All` (`scripts/azure/lib/graph.sh`, `GRAPH_SCOPE_VALUES`).
- **Owner decisions of 2026-10-18:**
  - The theme choice is saved with the person: it follows them to every browser and device, and the top bar's button saves it too.
  - Departments, Designations and Organisation chart each have a side-menu entry under Organisation that every employee sees; People stays administrators-only.
  - The Microsoft profile photo shows on My profile and replaces the initials on the account button; a person without a photo keeps their initials.
  - Inviting people one at a time from the directory stays Community; bringing in whole Entra groups and keeping them in step is the Enterprise item `integrations-entra-group-onboarding-and-sync`, argued from the market: adding the team is the first step of every trial, and the larger companies that pay value the group sync.

## Decision

- **The caller's own record: three signed-in routes** of the users module (`Profile/`), which need no permission besides a session, because each reads or changes the caller's own record only (`ModuleCatalogTests.SignedInRoutes`):
  - `GET /api/identity/users/me` (`Identity.Users.GetOwnProfile`) answers `ProfileResponse { id, displayName, workEmail, employeeCode, phoneNumber, dateOfJoining, departmentName, designationName, managerDisplayName, roles, theme }`: the record linked to the caller's object id (`IActorContext.ActorId`), today's position, and the built-in roles the caller holds in `ErpRole` order (`ICallerRoles` of `BuildingBlocks.Endpoints`, over `CallerRoles.Of`); 404 `user.not-found` when no record is linked.
  - `GET /api/identity/users/me/photo` (`Identity.Users.GetOwnPhoto`) answers the caller's photo, read on their behalf with their own delegated `User.Read` token (`IPeopleDirectory.OwnPhotoAsync`, `GraphPeopleDirectory`): the 240x240 size first, the largest otherwise, at most 1 MiB, served as the image type its leading bytes show (JPEG, PNG, GIF or WebP), because the API sends `nosniff`. A person without a photo is answered 204 rather than 404: the web app shows the photo as an image on every page, a browser reports every image answered 400 or above as a failed load, and an image without content leaves the initials in place. 403 `directory.access-denied` and 503 `directory.unavailable` as for the directory search.
  - `PUT /api/identity/users/me/theme` (`Identity.Users.ChooseOwnTheme`) takes `{ theme }` (`system`, `light` or `dark`) and answers 204; the last choice wins, without a version, because it is the person's own setting.
- **The photo is cached twice.** `GraphPeopleDirectory` keeps each person's outcome, the photo or that there is none, for 15 minutes in the in-process `HybridCache`, keyed by the object id; a refusal or an unavailable directory is never kept, so the next request asks again. The response is `Cache-Control: private, max-age=3600` with `Vary: Authorization, Cookie` (`WithCallerDataCaching`, beside `WithReferenceDataCaching`, whose data is the same for every caller), so a browser reuses the photo only for the same session cookie or bearer token, and another person signed in on the same browser never sees it.
- **The theme lives on the record.** `User.Theme` (`UserTheme`: `System` 1, `Light` 2, `Dark` 3, a `tinyint`; the wire enum is `ThemePreference` of the contracts, as `PersonStatus` beside `UserStatus`), migration `AddThemePreference` (existing rows `System`), left out of the change records (`ExcludeFromChangeRecords`), because it is a display setting and not a fact about the person.
- **The web app keeps the theme in step.** `ThemePreference` (`features/identity/users/profile`), mounted once by `AccountArea` behind `Suspense` while the person is signed in and the users module is on, applies the saved theme when the browser shows another, and saves every later change of the theme, whether made with the top bar's `ThemeToggle` (unchanged) or on My profile, through the Server Function `saveTheme`. Its rules are the pure `theme-preference-state.ts`. A choice whose save has not answered when its page is left is noted in `localStorage` (`erp-theme-unsaved`, kept for two minutes), so the next page saves it rather than putting the older saved theme back. `packages/ui` exports `useThemeChoice` for it, because `next-themes` is a dependency of the UI package alone.
- **Menu marking.** People's base path holds the three new ones, so `currentPage` marks an entry as the section only when no longer base path holds the address, and `breadcrumbTrail` ends on the longest matching base path: `/identity/users/departments` reads Home › Organisation › Departments, while `/identity/users/<id>` stays under People.
- **The pages:**
  - People gains "Invite from the directory" for holders of `identity.users.people.register`: a search step, which sends only text the API accepts after a 250 ms pause and marks the people the ERP already holds, and a details step, which invites with an `Idempotency-Key` and opens the new person's page; the dialog says the person still needs an app role of the ERP in Microsoft Entra.
  - `/identity/users/departments` draws the tree as nested lists with disclosure buttons, folded below the top level, and lets the holders of each permission add, rename, move and delete departments; `/identity/users/designations` does the same over a flat list. Both link each record's change history for holders of `platform.audit.changes.read`.
  - `/identity/users/org-chart` nests the people of a day under their managers, 20 people at the top a page, filtered by a department and those under it, and lets holders of `identity.users.positions.read` choose the date; the address holds the view.
  - `/profile` (My profile, from the account menu) shows the caller's record, photo, roles and a theme chooser, and the account button shows the photo over the initials.
- **Dates in query strings** are Kiota's own `DateOnly` (`dateOnly` of `@dewiride/erp-api-client`, `queryDateOf`), because a request builder writes a query date only when it is that class; request bodies keep `apiDateOf`.

## Options rejected

- **A permission for one's own record** (for example `identity.users.profile.read`, held by Employee): every signed-in person holds Employee or Administrator, so it would be held by everyone and add a sentence for the owner to approve; a signed-in route says the same directly.
- **A table of preferences:** one more entity with its change key and a row per person, for one value.
- **Saving the theme from `ThemeToggle`:** `packages/ui` cannot import `apps/web`, and the toggle stays as generated; the keeper watches the theme instead.
- **The photo through `User.ReadBasic.All`:** it works, but `User.Read` is the narrower of the held scopes for the caller's own photo.
- **No server-side cache of the photo:** the browser keeps a found photo for an hour, but a person without one would cost a Graph call on every full page load.
- **`WithReferenceDataCaching` for the photo:** without `Vary`, a browser shared by two people would show the first person's photo to the second for up to an hour.
- **404 for a missing photo:** every page of a person without a photo would log a failed image load.
- **Base paths outside `/identity/users` for the new pages:** they belong to the users module and its gated segment; the menu marks the most specific entry instead.

## Consequences

- Every signed-in person, whatever their roles, can read their own record and photo and save their theme; the API never answers another person's.
- A person's Microsoft photo appears in the ERP at most 15 minutes after the directory first answered for them on that API process, and a browser keeps it for up to an hour; a photo removed in Microsoft 365 lingers as long.
- A full page load of a signed-in person reads their profile once more (the keeper) and their photo (the account button); the photo request reaches Graph at most once per person every 15 minutes per API process.
- The theme a person last chose applies in every browser they sign in on from their next full page load there; two browsers changing it at once leave the last save.
- The Enterprise group onboarding builds on the invitation contract that `user-management-user-lifecycle` publishes.
