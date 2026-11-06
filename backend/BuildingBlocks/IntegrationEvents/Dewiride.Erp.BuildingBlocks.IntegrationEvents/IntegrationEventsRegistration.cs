using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Relay;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Retention;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Persistence.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents;

public static class IntegrationEventsRegistration
{
    public static IHostApplicationBuilder AddErpIntegrationEvents(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<IntegrationEventsOptions>()
            .BindConfiguration(IntegrationEventsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.TryAddSingleton(provider => new IntegrationEventCatalog(provider.GetServices<IntegrationEventSubscription>()));
        builder.Services.TryAddSingleton<IntegrationEventTables>();
        builder.Services.TryAddScoped<IIntegrationEventPublisher, OutboxPublisher>();
        builder.Services.AddJobRunSource<OutboxRelay>();
        builder.Services.AddRetentionSweep<IntegrationEventRetentionSweep>();

        return builder;
    }
}
