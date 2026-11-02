namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints.Responses;

/// <summary>A job run with every step of its history.</summary>
/// <param name="Run">The run as it stands.</param>
/// <param name="Events">The steps of the run, oldest first.</param>
internal sealed record JobRunHistoryResponse(JobRunResponse Run, IReadOnlyList<JobRunEventResponse> Events);
