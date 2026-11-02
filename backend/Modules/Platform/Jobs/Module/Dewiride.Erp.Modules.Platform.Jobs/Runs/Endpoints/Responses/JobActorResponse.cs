using Dewiride.Erp.BuildingBlocks.Jobs.Administration;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints.Responses;

/// <summary>Who enqueued a job run or took a step of its history.</summary>
/// <param name="Kind">Whether a person, the ERP itself or someone not signed in.</param>
/// <param name="Id">Microsoft Entra object id of the person, or the fixed id of the ERP or of someone not signed in.</param>
/// <param name="Name">Name the person's record has today; null for the ERP, for someone not signed in and when no record names the person.</param>
internal sealed record JobActorResponse(JobActorKind Kind, Guid Id, string? Name);
