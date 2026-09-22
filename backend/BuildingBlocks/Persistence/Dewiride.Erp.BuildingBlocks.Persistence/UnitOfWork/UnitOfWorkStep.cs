using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

internal sealed class UnitOfWorkStep(IServiceProvider services, DbContextCatalog catalog, DomainEventDispatcher dispatcher, UnitOfWorkSignal signal) : IPipelineStep
{
    public PipelineStage Stage => PipelineStage.UnitOfWork;

    public Task<Result<TResult>> InvokeAsync<TRequest, TResult>(TRequest request, HandlerDescriptor descriptor, PipelineContinuation<TResult> next, CancellationToken cancellationToken)
        where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(next);

        if (descriptor.Kind != HandlerKind.Command)
        {
            return next(cancellationToken);
        }

        var registration = catalog.Registrations.FirstOrDefault(candidate => candidate.ContextType.Assembly == descriptor.HandlerType.Assembly);
        if (registration is null)
        {
            return next(cancellationToken);
        }

        return EfUnitOfWork.RunAsync((DbContext)services.GetRequiredService(registration.ContextType), dispatcher, signal, next, cancellationToken);
    }
}
