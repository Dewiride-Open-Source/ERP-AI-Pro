namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints.Responses;

/// <summary>A kind of request the ERP asks approval for, with the administrators' setting.</summary>
/// <param name="Kind">Key of the kind, which names it in its requests and its setting.</param>
/// <param name="Title">What a request of the kind is called, such as Bill approval.</param>
/// <param name="Description">What a request of the kind approves, in one sentence.</param>
/// <param name="Steps">The steps of a request of the kind, in the order they are decided.</param>
/// <param name="DeclaredWorkingDays">Working days each step has while the administrators have set up no other number.</param>
/// <param name="Setting">The administrators' setting; null until one is set up, while the kind needs approval and gives each step its declared working days.</param>
internal sealed record ApprovalKindResponse(string Kind, string Title, string Description, IReadOnlyList<ApprovalKindStepResponse> Steps, int DeclaredWorkingDays, ApprovalKindSettingResponse? Setting);
