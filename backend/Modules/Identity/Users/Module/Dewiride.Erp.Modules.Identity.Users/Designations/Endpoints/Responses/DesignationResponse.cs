namespace Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints.Responses;

/// <summary>A job title a person's position can name.</summary>
/// <param name="Id">Identifier of the designation.</param>
/// <param name="Name">Name of the designation, unique among the designations.</param>
/// <param name="Version">Version of the designation, which a change names to be refused when the designation changed since.</param>
internal sealed record DesignationResponse(Guid Id, string Name, string Version);
