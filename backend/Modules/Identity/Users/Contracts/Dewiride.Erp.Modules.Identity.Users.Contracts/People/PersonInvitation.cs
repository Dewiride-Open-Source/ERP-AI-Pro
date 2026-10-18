namespace Dewiride.Erp.Modules.Identity.Users.Contracts.People;

// The display name and the sign-in name are the ones the company directory holds for the account, so the person's first
// sign-in finds the record by its object id; the sign-in name becomes the record's work email.
public sealed record PersonInvitation(Guid EntraObjectId, string DisplayName, string SignInName, string? EmployeeCode, IReadOnlyCollection<PersonRole> Roles);
