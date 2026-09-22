using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Pipeline;

internal sealed class QueryHandlerPipeline<TQuery, TResult>(HandlerDescriptor descriptor, IQueryHandler<TQuery, TResult> handler, IEnumerable<IPipelineStep> steps) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return PipelineRunner.RunAsync(steps, query, descriptor, token => handler.HandleAsync(query, token), cancellationToken);
    }
}
