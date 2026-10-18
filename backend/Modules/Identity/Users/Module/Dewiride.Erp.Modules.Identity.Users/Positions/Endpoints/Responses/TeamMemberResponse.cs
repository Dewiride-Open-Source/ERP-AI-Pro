namespace Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;

/// <summary>A person who reports to another today or from a later date.</summary>
/// <param name="Id">Identifier of the person's record.</param>
/// <param name="DisplayName">Name the ERP shows for the person.</param>
internal sealed record TeamMemberResponse(Guid Id, string DisplayName);
