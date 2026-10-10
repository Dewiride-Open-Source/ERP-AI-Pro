# Identity / Users

The people who use the ERP: one record per person, created at their first sign-in or registered beforehand by an administrator, and consulted at every sign-in and on every request of a session or a person's bearer token ([ADR-0039](../../../../docs/adr/0039-people-and-their-admission.md)).

| Aspect | Value |
|---|---|
| Assembly | `Dewiride.Erp.Modules.Identity.Users` |
| Schema | `identity_users` (`UsersDbContext`, table `Users`: the Entra object id, display name and work email Entra keeps, the employee code, phone number, designation and date of joining administrators keep, the status and the last sign-in time; soft-deletable, versioned, audited) |
| Route group | `/api/identity/users`, every route for holders of the `Erp.Admin` app role: `GET /` (paged, sorted, filtered), `GET /{id}`, `POST /` (with `Idempotency-Key`), `PUT /{id}` and `PUT /{id}/status` (each with the record's `version`), `DELETE /{id}` |
| Feature flag | `Erp.Modules.Identity.Users` (the routes only; admission runs whatever the flag says) |
| Contracts | `UsersPermissions` (`identity.users.people.read`, `.register`, `.update`, `.change-status`, `.delete`, enforced from `user-management-roles-and-permissions`), `PersonStatus` |
| Integration | `UserAdmission` implements `IPersonAdmission` of `BuildingBlocks.Application`: `AdmitAsync` creates or refreshes the person's record at every sign-in and refuses a deactivated one, `IsAdmittedAsync` answers whether an active record names the Entra object id |
| Events | none |
