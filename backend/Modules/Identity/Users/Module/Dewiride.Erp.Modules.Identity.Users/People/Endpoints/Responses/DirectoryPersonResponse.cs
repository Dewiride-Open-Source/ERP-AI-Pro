namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;

/// <summary>A person of the company directory in Microsoft Entra.</summary>
/// <param name="EntraObjectId">Object id of the person's Entra account, which an invitation names.</param>
/// <param name="DisplayName">Name the directory shows for the person.</param>
/// <param name="SignInName">Address the person signs in with, which becomes the work email of the record an invitation creates: their user principal name, or the mail of a guest of another organisation.</param>
/// <param name="UserPrincipalName">User principal name of the person's account in the directory.</param>
/// <param name="Mail">Email address of the person's mailbox, when they have one.</param>
/// <param name="PersonId">Id of the person's record when the ERP already holds them: the record linked to their account, or one registered with their sign-in name and linked to no account yet.</param>
internal sealed record DirectoryPersonResponse(
    Guid EntraObjectId,
    string DisplayName,
    string SignInName,
    string UserPrincipalName,
    string? Mail,
    Guid? PersonId);
