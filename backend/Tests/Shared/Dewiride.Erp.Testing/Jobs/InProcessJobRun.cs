using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Testing.Jobs;

public sealed record InProcessJobRun(JobRunId Id, string JobName, string IdempotencyKey, object? Payload, Result? Result);
