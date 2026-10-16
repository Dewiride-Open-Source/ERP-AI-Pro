using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Responses;

/// <summary>A recorded change: what changed, who made the change, when and from where.</summary>
/// <param name="Id">Identifier of the change record.</param>
/// <param name="OccurredAt">UTC time of the change, the time the changed record's own audit stamp holds.</param>
/// <param name="Operation">What the change did to the record.</param>
/// <param name="EntityType">Change key of the type of the changed record, such as identity.users.people.</param>
/// <param name="EntityId">Identifier of the changed record.</param>
/// <param name="SubjectType">Change key of the type of the record the change is filed under: the changed record itself, or for a position its person.</param>
/// <param name="SubjectId">Identifier of the record the change is filed under.</param>
/// <param name="SubjectName">Name that record has today; null when none is found.</param>
/// <param name="ActorKind">Whether a person, the ERP itself or someone not signed in made the change.</param>
/// <param name="ActorId">Microsoft Entra object id of the person who made the change, or the fixed id of the ERP or of someone not signed in.</param>
/// <param name="ActorName">Name the person's record has today; null for the ERP, for someone not signed in and when no record names them.</param>
/// <param name="ClientAddress">Client address of the request that made the change; null when the change came from no request with an address, as work the ERP does on its own.</param>
/// <param name="CorrelationId">Correlation id of the request that made the change, the X-Correlation-ID its answer carried, or the trace id of the ERP's own work.</param>
/// <param name="Changes">The changed fields with their values before and after; empty when the change records none, as a deletion that changed nothing else.</param>
internal sealed record ChangeRecordResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    ChangeOperation Operation,
    string EntityType,
    Guid EntityId,
    string SubjectType,
    Guid SubjectId,
    string? SubjectName,
    ChangeActorKind ActorKind,
    Guid ActorId,
    string? ActorName,
    string? ClientAddress,
    string? CorrelationId,
    IReadOnlyList<ChangedValueResponse> Changes);
