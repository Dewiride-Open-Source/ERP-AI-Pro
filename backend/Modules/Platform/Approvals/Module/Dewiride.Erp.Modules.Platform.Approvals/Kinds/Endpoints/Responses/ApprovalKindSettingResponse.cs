namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints.Responses;

/// <summary>The administrators' setting of a kind of request, which applies to the requests submitted after it was made.</summary>
/// <param name="Id">Identifier of the setting.</param>
/// <param name="Required">Whether the kind's records need approval; while false each goes through at once, without a request.</param>
/// <param name="WorkingDaysToDecide">Working days each step of the kind's requests has.</param>
/// <param name="Version">Version of the setting, sent back with a change.</param>
internal sealed record ApprovalKindSettingResponse(Guid Id, bool Required, int WorkingDaysToDecide, string Version);
