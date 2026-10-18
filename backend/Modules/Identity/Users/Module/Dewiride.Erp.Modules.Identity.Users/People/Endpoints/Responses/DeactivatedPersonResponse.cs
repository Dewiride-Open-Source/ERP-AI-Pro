using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;

/// <summary>A person the check of the company directory deactivated.</summary>
/// <param name="Id">Identifier of the person's record.</param>
/// <param name="DisplayName">Name the ERP shows for the person.</param>
/// <param name="DeactivatedBecause">Whether the person's Entra account was found disabled or deleted.</param>
internal sealed record DeactivatedPersonResponse(Guid Id, string DisplayName, PersonDeactivationReason DeactivatedBecause);
