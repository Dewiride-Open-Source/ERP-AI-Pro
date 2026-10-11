# Identity / Users

The people who use the ERP: one record per person, created at their first sign-in, registered beforehand by an administrator by work email or invited from the company directory in Microsoft Entra, and consulted at every sign-in and on every request of a session or a person's bearer token ([ADR-0039](../../../../docs/adr/0039-people-and-their-admission.md), [ADR-0040](../../../../docs/adr/0040-directory-invitations.md)), and the roles each person holds in the ERP besides those their app roles give ([ADR-0041](../../../../docs/adr/0041-roles-and-permissions.md)).

| Aspect | Value |
|---|---|
| Assembly | `Dewiride.Erp.Modules.Identity.Users` |
| Schema | `identity_users` (`UsersDbContext`, table `Users`: the Entra object id, display name and work email Entra keeps, the employee code, phone number, designation and date of joining administrators keep, the status, the roles assigned in the ERP (`Roles`, a `json` array of `Finance`, `Hr` and `Manager`) and the last sign-in time; soft-deletable, versioned, audited) |
| Route group | `/api/identity/users`, each route requiring its permission, which administrators alone hold: `GET /` (paged, sorted, filtered) and `GET /{id}` (`read`), `POST /` (by work email, with `Idempotency-Key`), `GET /directory?search=` (the company directory, read through `IPeopleDirectory` on the administrator's behalf) and `POST /invitations` (a directory person by Entra object id, with `Idempotency-Key`) (`register`; roles named in either also need `assign-roles`), `PUT /{id}` (`update`), `PUT /{id}/status` (`change-status`) and `PUT /{id}/roles` (`assign-roles`), each with the record's `version`, `DELETE /{id}` (`delete`) |
| Feature flag | `Erp.Modules.Identity.Users` (the routes only; admission runs whatever the flag says) |
| Contracts | `UsersPermissions` (`identity.users.people.read`, `.register`, `.update`, `.change-status`, `.delete`, `.assign-roles`, declared for Administrator alone), `PersonStatus`, `PersonRole` (`finance`, `hr`, `manager`: the roles the ERP assigns) |
| Integration | `UserAdmission` implements `IPersonAdmission` of `BuildingBlocks.Application`: `AdmitAsync` creates or refreshes the person's record at every sign-in and refuses a deactivated one, `AdmissionOfAsync` answers the roles assigned to the active record that names the Entra object id, or nothing when no such record admits the person |
| Directory | `IPeopleDirectory` of `BuildingBlocks.Application`, implemented by `BuildingBlocks.Authentication` over Microsoft Graph; the endpoints read it and hand the people to `FindRegisteredPeopleQuery` and `InvitePersonCommand`, so no handler needs a signed-in person |
| Events | none |
