using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Pipeline;

internal sealed class CommandHandlerPipeline<TCommand, TResult>(HandlerDescriptor descriptor, ICommandHandler<TCommand, TResult> handler, IEnumerable<IPipelineStep> steps) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return PipelineRunner.RunAsync(steps, command, descriptor, token => handler.HandleAsync(command, token), cancellationToken);
    }
}
