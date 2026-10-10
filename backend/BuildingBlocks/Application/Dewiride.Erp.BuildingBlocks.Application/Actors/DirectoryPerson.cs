namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

public sealed record DirectoryPerson(Guid ObjectId, string DisplayName, string UserPrincipalName, string? Mail);
