using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Pipeline;

internal static class PipelineRunner
{
    public static Task<Result<TResult>> RunAsync<TRequest, TResult>(
        IEnumerable<IPipelineStep> steps,
        TRequest request,
        HandlerDescriptor descriptor,
        PipelineContinuation<TResult> handler,
        CancellationToken cancellationToken)
        where TRequest : notnull
    {
        var chain = handler;
        foreach (var step in steps.OrderByDescending(step => step.Stage))
        {
            var next = chain;
            chain = token => step.InvokeAsync(request, descriptor, next, token);
        }

        return chain(cancellationToken);
    }
}
