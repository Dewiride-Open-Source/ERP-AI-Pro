namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

public sealed record DirectoryPerson(Guid ObjectId, string DisplayName, string SignInName, string UserPrincipalName, string? Mail);
