using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Pipeline;

public interface IPipelineStep
{
    PipelineStage Stage { get; }

    Task<Result<TResult>> InvokeAsync<TRequest, TResult>(TRequest request, HandlerDescriptor descriptor, PipelineContinuation<TResult> next, CancellationToken cancellationToken)
        where TRequest : notnull;
}
