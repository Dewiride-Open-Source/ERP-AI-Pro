using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Pipeline;

public delegate Task<Result<TResult>> PipelineContinuation<TResult>(CancellationToken cancellationToken);
