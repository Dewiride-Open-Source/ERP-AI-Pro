using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;

/// <summary>A person who uses the ERP.</summary>
/// <param name="Id">Identifier of the person's record.</param>
/// <param name="EntraObjectId">Object id of the person's Entra account, once their first sign-in linked it or an administrator gave it.</param>
/// <param name="DisplayName">Name the ERP shows for the person, which each sign-in brings from Entra.</param>
/// <param name="WorkEmail">Work email of the person, in lower case, which each sign-in brings from Entra.</param>
/// <param name="EmployeeCode">Employee code, unique among the people of the ERP.</param>
/// <param name="PhoneNumber">Phone number in international form, + and its country code first.</param>
/// <param name="Designation">Designation, as free text.</param>
/// <param name="DateOfJoining">Date the person joined the company.</param>
/// <param name="Status">Whether the person may sign in.</param>
/// <param name="LastSignedInAt">UTC time of the person's last sign-in.</param>
/// <param name="CreatedAt">UTC time the record was created.</param>
/// <param name="Version">Version of the record, which an edit or a status change names to be refused when the record changed since.</param>
internal sealed record PersonResponse(
    Guid Id,
    Guid? EntraObjectId,
    string DisplayName,
    string WorkEmail,
    string? EmployeeCode,
    string? PhoneNumber,
    string? Designation,
    DateOnly? DateOfJoining,
    PersonStatus Status,
    DateTimeOffset? LastSignedInAt,
    DateTimeOffset CreatedAt,
    string Version);
