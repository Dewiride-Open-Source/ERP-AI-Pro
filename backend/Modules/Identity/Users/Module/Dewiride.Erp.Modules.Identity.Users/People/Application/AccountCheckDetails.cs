namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

internal sealed record AccountCheckDetails(int CheckedCount, IReadOnlyList<DeactivatedPersonDetails> Deactivated);
