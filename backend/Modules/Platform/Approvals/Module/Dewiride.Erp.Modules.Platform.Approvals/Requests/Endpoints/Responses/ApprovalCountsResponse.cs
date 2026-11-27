namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Responses;

/// <summary>How many approval requests concern the caller now.</summary>
/// <param name="WaitingForMe">Requests that wait for the caller's decision, as the waiting box lists them.</param>
/// <param name="LateForMe">Of those, the requests whose waiting step is past its due time.</param>
/// <param name="MineWaiting">Requests the caller asked for or submitted that still wait for a decision.</param>
internal sealed record ApprovalCountsResponse(int WaitingForMe, int LateForMe, int MineWaiting);
