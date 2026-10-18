using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

internal sealed record DeactivatedPersonDetails(Guid Id, string DisplayName, PersonDeactivationReason DeactivatedBecause);
