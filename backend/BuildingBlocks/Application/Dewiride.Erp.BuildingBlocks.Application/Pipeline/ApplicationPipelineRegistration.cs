using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline.Steps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dewiride.Erp.BuildingBlocks.Application.Pipeline;

public static class ApplicationPipelineRegistration
{
    public static IServiceCollection AddApplicationPipeline(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPipelineStep, LoggingStep>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPipelineStep, ValidationStep>());
        services.TryAddScoped<DomainEventDispatcher>();

        return services;
    }
}
