using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;

internal sealed record CompletedAttempt(ClaimedRun Run, JobAttemptOutcome Outcome, JobRunStatus? RecordedStatus);
