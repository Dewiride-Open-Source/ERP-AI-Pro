namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;

/// <summary>What a check of the Entra accounts of the company directory found and changed.</summary>
/// <param name="CheckedCount">Number of active people linked to an Entra account whose account the check read.</param>
/// <param name="Deactivated">People the check deactivated because their account is disabled or deleted, by display name.</param>
internal sealed record DirectoryAccountCheckResponse(int CheckedCount, IReadOnlyList<DeactivatedPersonResponse> Deactivated);
