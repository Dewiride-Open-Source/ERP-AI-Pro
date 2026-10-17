# ADR-0045: People pages and the permission catalogue

Status: accepted
Date: 2026-10-17

## Context

- **What existed.**
  - The users module had its routes: people, their roles, departments, designations, positions and the organisation chart (ADR-0039 to ADR-0042). Every change to a person was a change record (ADR-0043). The web app had no page for any of them.
  - The permission catalogue (ADR-0041) held one `PermissionDeclaration(Name, HeldBy)` per permission. The authorization handler read it, and the OpenAPI document listed its names in the enum `Permission`. No route answered it, and nothing said in words what a permission allows.
- **The roadmap scope** asked for:
  - the users list, on the data table, with search, sorting, filters, pages and cards on phones;
  - a person's page, and the forms to register and edit a person;
  - a dialog to assign roles;
  - a page that shows what each role may do;
  - Playwright clicking every control on desktop and phones, in light and dark, with screenshots.
- **What earlier items left for this one** (the roadmap notes):
  - A Radix `Select` fills an empty `SelectValue` from its item only once it has mounted, so its trigger widens as the page hydrates.
  - The edit forms map `concurrency.conflict`, `user.own-status` and `user.own-deletion`. The name and work email are read-only, because every sign-in takes them from Entra.
  - The roles page needs a route that answers the catalogue.
  - Next.js 16 streams metadata to browsers. A title that waits for a read the shell does not wait for, such as a person's name, arrives after the page.
- **Owner decisions of 2026-10-17:**
  - Moves are recorded and withdrawn on the person's page: a "Record a move" dialog takes the effective date, department, designation and manager, and each move of the history can be withdrawn. The pickers are built so that `user-management-web-invitations-organisation-and-my-profile` can reuse them.
  - The roles and permissions page has no sidebar entry. It lives at `/identity/users/permissions` and is linked from the People page and from each person's roles section.
  - On an administrator's own page, the note "This is your own record. Another administrator can deactivate or delete it." replaces Deactivate, Reactivate and Delete.
  - The sidebar heading of the people pages is "Organisation".

## Decision

- **Every permission says what it allows.**
  - A declaration is `PermissionDeclaration(Name, HeldBy, Description)`. The description is one plain sentence saying what a holder may do, written for the people who assign roles. The owner approves each sentence in the pull request that adds it.
  - `PermissionCatalog` refuses a blank description when the host starts. `ModuleCatalogTests.ModuleDescriptors_DescribeEveryPermissionInOneSentence` (architecture) checks that every description is one sentence: a capital letter first, a full stop last, and no other full stop, question mark, exclamation mark or line break. `ModuleDescriptors_DescribeNoTwoPermissionsAlike` checks that no two permissions share a description, ignoring case, because the roles page names each row by its description alone.
  - `PermissionCatalog.Permissions` lists a `PermissionCatalogEntry(Name, Description, HeldBy)` per permission, in ordinal order of the name. `HeldBy` follows the order of `ErpRole` and always includes Administrator, who holds every permission. The type's name ends in `Entry` because code analysis rule CA1711 keeps the suffix `Permission` for the permission types of `System.Security`.
- **The catalogue route.** `GET /api/identity/users/permissions` (`Identity.Users.ListPermissions`) answers `PermissionCatalogResponse { roles, permissions: [{ name, description, heldBy }] }`.
  - `roles` is every built-in role in the order of `ErpRole`: `employee`, `finance`, `hr`, `manager` and `administrator`, the enum schema `ErpRole` of the OpenAPI document. A role that holds nothing today, such as Finance, still has its column.
  - `ListPermissionsHandler` answers the catalogue the host built at startup; nothing about roles is stored.
  - The route requires `identity.users.people.read`, because the page belongs to the people pages and serves those who assign roles. A permission of its own would be held by the same people. An employee is answered 403 `request.forbidden`.
  - `name` stays a string. The enum `Permission` belongs to `CurrentUserResponse`, whose transformer lives in `BuildingBlocks.Authentication`, which cannot name a module's response.
- **The catalogue today:**

  | Permission | Held by | Description |
  |---|---|---|
  | `identity.users.departments.create` | Administrator | Add a department, at the top or under another department. |
  | `identity.users.departments.delete` | Administrator | Delete a department that holds no other department and that nobody is or will be in. |
  | `identity.users.departments.read` | Employee, Administrator | See the departments and which department each one sits under. |
  | `identity.users.departments.update` | Administrator | Rename a department or move it under another department. |
  | `identity.users.designations.create` | Administrator | Add a designation. |
  | `identity.users.designations.delete` | Administrator | Delete a designation that nobody holds or will hold. |
  | `identity.users.designations.read` | Employee, Administrator | See the designations people can hold. |
  | `identity.users.designations.update` | Administrator | Rename a designation. |
  | `identity.users.org-chart.read` | Employee, Administrator | See today's organisation chart: who is in each department and who reports to whom. |
  | `identity.users.people.assign-roles` | Administrator | Give people the Finance, HR and Manager roles or take them away, also when registering or inviting them. |
  | `identity.users.people.change-status` | Administrator | Deactivate a person, which signs them out and stops them signing in, or reactivate them. |
  | `identity.users.people.delete` | Administrator | Delete a person's record, which ends their open sessions. |
  | `identity.users.people.read` | Administrator | See the people registered in the ERP, the details of each one and what each role may do. |
  | `identity.users.people.register` | Administrator | Register a person before their first sign-in, by work email or from the company directory. |
  | `identity.users.people.update` | Administrator | Change a person's employee code, phone number and date of joining. |
  | `identity.users.positions.read` | Administrator | See each person's past and planned moves, and the organisation chart of any date. |
  | `identity.users.positions.record` | Administrator | Record a person's move to a department, designation or manager from a date. |
  | `identity.users.positions.withdraw` | Administrator | Withdraw a move recorded for a person. |
  | `platform.attachments.files.delete` | Administrator | Delete stored files. |
  | `platform.attachments.files.read` | Employee, Administrator | See the stored files and download them. |
  | `platform.attachments.files.upload` | Employee, Administrator | Upload files. |
  | `platform.audit.changes.export` | Administrator | Download the audit trail as a CSV file. |
  | `platform.audit.changes.read` | Administrator | See the audit trail of who changed what in the ERP, and when. |
  | `platform.system-info.info.read` | Administrator | See which version of the ERP is running and how long it has been up. |
  | `platform.system-info.startups.read` | Administrator | See the ERP's most recent starts and the version each one ran. |

- **Navigation.**
  - `features/identity/_shared/identity-area.ts` declares the area `identity`, titled "Organisation".
  - `peopleNavigation` is "People": `/identity/users`, flag `Erp.Modules.Identity.Users`, permission `identity.users.people.read`, `UsersIcon`. The registry lists it after the audit trail.
  - The pages below `/identity/users` have no entries of their own. People stays the marked entry (`aria-current="true"` below its own page), and their breadcrumbs end at People, a link: Home, Organisation, People. No page adds a step of its own.
- **The pages.** `app/(app)/identity/users/layout.tsx` gates the segment with `requireFeature`, and each page wraps its content in `RequirePermission`:

  | Route | Permission | Shows |
  |---|---|---|
  | `/identity/users` | `identity.users.people.read` | the people list |
  | `/identity/users/new` | `identity.users.people.register` | the registration form |
  | `/identity/users/[userId]` | `identity.users.people.read` | a person's details, today's position, their moves, roles and recent changes |
  | `/identity/users/[userId]/edit` | `identity.users.people.update` | the edit form |
  | `/identity/users/permissions` | `identity.users.people.read` | what each role may do |

- **The people list** (`features/identity/users/people`).
  - The list kit, read on the server, its view in the address.
  - 10, 20, 50 or 100 people a page, 10 by default, because a full-page capture of 20 cards on an iPhone passes the 16,384-pixel limit.
  - Sorted by display name unless the person sorts by work email, employee code, department, designation, manager, date of joining, status or last sign-in.
  - Filtered by name, work email, employee code, status, department, designation and a date range of joining.
  - When the departments or designations cannot be read and the address chooses none of them, their filter is hidden and a note above the list says it cannot be filtered by them now. When the address chooses some, the filter stays and shows each as "Department (name not available)" or "Designation (name not available)"; "Unknown department" and "Unknown designation" name only an id that a lookup which was read does not list (`OrganisationLookup`, `filterOptions` and `filterNote` in `people/components/person-rows.ts`).
  - The status is one `PersonStatusBadge` on the list and the person page alike, and the roles column is headed "Roles in the ERP", because an administrator's role comes from Microsoft Entra and the column would otherwise read as holding nothing.
  - Cards below the data table's `@2xl` container width.
  - The `(list)` route group keeps the list's loading skeleton off the pages below it.
  - The header links to "Register a person", for holders of `register`, and to "Roles and permissions".
- **The person page** (`features/identity/users/details`).
  - A malformed id answers the not-found page (`z.uuid`).
  - The title is the person's name from the read the page itself makes, deduplicated with React's `cache`. The detail and edit pages have no `loading.tsx`, so an unknown id answers a real 404 and the title arrives in the head.
  - Deactivate, Reactivate and Delete ask through `ConfirmDialog`. A refusal shows beside its button in `role="alert"`, because the dialog closes when it is confirmed.
  - Delete's question follows the person's status (`deletion(name, status)`), because a deleted record admits no one and the next sign-in through Microsoft Entra creates a new, active one (`DeletePersonHandler`, `AdmitPersonHandler`, ADR-0039). For an active person it says that deactivating, not deleting, keeps them out. For a deactivated person it says that being deactivated is what keeps them out and that deleting the record ends that, so keeping the record keeps them out.
  - On the administrator's own record the note replaces those buttons. The API still refuses `user.own-status` and `user.own-deletion`, and their messages stay for a page another tab left open.
  - The moves (`identity.users.positions.read`) list the person's positions. "Record a move" takes a date, a department, a designation and a manager, each but the date optional. Departments and designations come from the server as combobox options, and the manager is searched through the Server Function `searchManagers`, which leaves the person out. Each move can be withdrawn.
  - Neither form dialog, Record a move or Assign roles, closes while it saves. Escape, a click outside, the close button and Cancel do nothing until the answer arrives, and Cancel and the close button are `aria-disabled` meanwhile (`useSavingDialog` and `useSavingReport` in `details/hooks/use-saving-dialog.ts`; the dialogs draw their own close button, `SavingDialogCornerClose`, because the generated `DialogContent`'s takes no props). So a refusal always reaches the open form, and a save the person tried to abandon never toasts after its dialog has gone.
  - Withdrawing asks first. The question says the move before it applies again or, for the earliest move, that the person has no department, designation or manager from that date until a later move starts. When the API refuses, focus stays on Withdraw beside the refusal; once the move is gone, focus moves to the Moves heading.
  - With no department or designation set up, the dialog's pickers say "No departments are set up yet." and "No designations are set up yet." rather than "No department matches.".
  - The latest ten changes, for holders of `platform.audit.changes.read` while the audit module is on, are the `RecordHistory` server component of `@/features/platform/audit`, with a link to `/platform/audit?record=<id>`. The audit timeline's entry rendering moves to `changes/components/change-entry.tsx`, shared by both, because `ChangeTimeline` renders `ListLink`, which needs a `ListNavigationProvider`. The person page ships no code of the audit export, which `web-bundles.spec.ts` checks on the administrator's own page. The audit trail calls a change of `identity.users.positions` a move, the word the pages use.
- **Changes to a person.** Each change is a form of the forms kit or a confirmed action, through Server Functions that start with the session and permission checks (rule X4):
  - registering (`registerPerson`, with an `Idempotency-Key`; naming roles needs `identity.users.people.assign-roles`), then the person's page, where a toast confirms it;
  - editing the employee code, phone number and date of joining, on a page of its own (`updatePerson`, with the record's version), then the person's page with a toast, because a saved edit and a cancelled one would otherwise land alike;
  - assigning Finance, HR and Manager in a dialog of three checkboxes keyed by the record's version (`assignPersonRoles`); the roles card, not the dialog, links to the roles page, so following it never discards ticked boxes;
  - recording a move (`recordPersonMove`, with an `Idempotency-Key`) and withdrawing one (`withdrawPersonMove`);
  - changing the status (`changePersonStatus`) and deleting (`deletePerson`).

  Registering and editing move on themselves: the Server Function answers `succeeded` (registration's `RegistrationAnswer` adds the new record's id and display name, both from the API's answer), and the form shows its toast and then replaces its page with the person's (`router.replace`), so Back skips the form.

  Every refusal the API defines is shown in plain words and changes nothing. Registration words `concurrency.conflict` too: it is the answer when another registration saved the same work email or employee code at the same moment. A move maps these codes:

  | Code | Shown as |
  |---|---|
  | `position.date-taken` | "A move from this date already exists; withdraw it first." |
  | `position.department-not-found`, `position.designation-not-found`, `position.manager-not-found` | "That choice no longer exists; choose again." |
  | `position.manager-inactive` | the manager is deactivated |
  | `position.self-managed` | the person cannot manage themself |
  | `position.reporting-cycle` | the move would make a reporting cycle |
  | `position.previous-no-longer-valid` | withdrawing would bring back a move that names something since deleted or deactivated |
  | `position.not-found` | the move was already withdrawn |
  | `concurrency.conflict` | "Another organisation change is being saved; try again." |

- **The roles page** (`features/identity/users/permissions`).
  - `PermissionsOverview` reads the catalogue once per request (`server/queries.ts`) and shows it read-only: "Roles and permissions", a link to the people list, and the card "What each role may do". When the API cannot answer, the card shows that the roles and permissions are not available.
  - `PermissionMatrix` is the `Table` primitive. A column header names each role in the order of `roles`. Each permission is a row whose row header is its description, which alone identifies the row; the permission's name stays off the page, because no one who reads it needs the code. Each cell reads "Held" or "Not held".
  - The table is named by the card's heading and becomes a focusable region when it scrolls sideways on a narrow screen.
  - `permission-rows.ts` holds the pure shaping of the matrix. The role labels (Employee, Finance, HR, Manager and Administrator) come from `people/components/person-text.ts`, the module's one copy, which the list, the person page and the dialogs use too.
- **Pieces shared across the item.**
  - `features/identity/users/organisation/server/queries.ts` serves the lookups the list filters and the move dialog share. `listDepartmentOptions` and `listDesignationOptions` read every page at 200 a page and sort by label. `searchActivePeople(text, { excluding })` answers at most 20 active people whose name contains the text. Each returns the shape of the combobox's options, built by `organisation-options.ts`.
  - `shared/api/dates/date-only.ts` turns the Kiota `DateOnly` into `yyyy-MM-dd` and back, because a Kiota `DateOnly` cannot cross from a Server Component to a Client Component, and a request needs one.
  - The form kit's server-answer select and the kitchen sink's quarter and currency specimens pass their value text to `SelectValue`, as the data table's selects do (`design-system.md`, Data table, Hydration).
  - The Playwright fixture `SignedInApi` gains `put`, and the gated pair turns `Erp.Modules.Identity.Users` off with the other module flags.
- **Earlier decisions this changes.**
  - ADR-0037: a page below a navigation entry adds no breadcrumb step of its own, and the roles page is reached without an entry.
  - ADR-0041: a declaration carries a description; the catalogue answers its entries, through a route of the users module; `identity.users.people.read` also reads the catalogue.
  - ADR-0043: the audit module exports `RecordHistory`, and its timeline entry is shared with it.
  - ADR-0025: a Server Function whose form moves on to the record it created may answer, with `succeeded`, that record's id and name as the API returned them (`RegistrationAnswer`). It still never echoes a submitted value.

## Options rejected

- **A permission of its own for the catalogue.** Whoever reads people reads what their roles allow, so a second permission would always be held with the first.
- **Descriptions kept in the web app.** A second table to keep true, keyed by names it cannot check, and a new permission would show without words until someone wrote them.
- **Editing roles on the page.** The roles are built in (owner decisions of 2026-10-11): what a role allows changes with the code.
- **Blocking metadata for every browser** (`htmlLimitedBots: /.*/`). Every page's first byte would wait for its slowest title read.
- **The person's name as a breadcrumb step.** Every person page would need a second read of the record, or the shell would need it from the page.

## Consequences

- A new permission needs a sentence the owner approves; without one the host does not start, and the architecture test checks its form. The roles page shows it on the next deployment.
- The roles page follows the code, so changing what a role allows is a change to a module descriptor and appears on the page with the release.
- Administrators alone read the catalogue today, because only they hold `identity.users.people.read`.
- `user-management-web-invitations-organisation-and-my-profile` reuses the organisation lookups and the move dialog's pickers for its pages.
