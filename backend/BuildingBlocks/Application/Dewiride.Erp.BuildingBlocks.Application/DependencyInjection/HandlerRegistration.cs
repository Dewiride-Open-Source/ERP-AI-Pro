using System.Reflection;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;

public static class HandlerRegistration
{
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        services.AddApplicationPipeline();
        foreach (var implementation in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }))
        {
            foreach (var service in implementation.GetInterfaces().Where(t => t.IsGenericType))
            {
                var definition = service.GetGenericTypeDefinition();
                if (definition == typeof(ICommandHandler<,>))
                {
                    AddPipeline(services, service, implementation, typeof(CommandHandlerPipeline<,>), HandlerKind.Command);
                }
                else if (definition == typeof(IQueryHandler<,>))
                {
                    AddPipeline(services, service, implementation, typeof(QueryHandlerPipeline<,>), HandlerKind.Query);
                }
                else if (definition == typeof(IDomainEventHandler<>))
                {
                    services.AddScoped(service, implementation);
                }
            }
        }

        return services;
    }

    private static void AddPipeline(IServiceCollection services, Type service, Type implementation, Type pipelineDefinition, HandlerKind kind)
    {
        var arguments = service.GetGenericArguments();
        var descriptor = new HandlerDescriptor(implementation, arguments[0], kind);
        var factory = ActivatorUtilities.CreateFactory(pipelineDefinition.MakeGenericType(arguments), [typeof(HandlerDescriptor), service]);
        services.TryAddScoped(implementation);
        services.AddScoped(service, provider => factory(provider, [descriptor, provider.GetRequiredService(implementation)]));
    }
}
