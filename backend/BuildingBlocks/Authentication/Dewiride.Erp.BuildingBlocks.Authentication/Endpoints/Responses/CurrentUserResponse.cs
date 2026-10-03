namespace Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Responses;

/// <summary>The signed-in person.</summary>
/// <param name="Id">Object id of the person in Microsoft Entra ID, the actor the API records for their changes.</param>
/// <param name="Name">Display name of the person.</param>
/// <param name="UserName">Name the person signs in with, usually their email address.</param>
/// <param name="Roles">App roles the person holds on the app registration, such as Erp.User and Erp.Admin.</param>
internal sealed record CurrentUserResponse(Guid Id, string Name, string UserName, IReadOnlyList<string> Roles);
