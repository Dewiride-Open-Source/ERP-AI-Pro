namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;

/// <summary>People of the company directory whose display name has a word starting with the search text, or whose email address or sign-in name starts with it.</summary>
/// <param name="People">At most 25 people, by display name.</param>
/// <param name="HasMore">Whether more people matched than were returned, so a longer search text would narrow them down.</param>
internal sealed record DirectorySearchResponse(IReadOnlyList<DirectoryPersonResponse> People, bool HasMore);
