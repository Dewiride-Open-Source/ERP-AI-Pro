using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Receipts;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Consumers;

public sealed class ShipmentLabelPrinter(SampleDbContext database, SubscriberScript script) : IIntegrationEventHandler<ShipmentDispatched>
{
    public async Task<Result> HandleAsync(ShipmentDispatched integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var outcome = await script.OutcomeOf(typeof(ShipmentLabelPrinter), integrationEvent, context, database, cancellationToken);
        if (outcome.IsSuccess)
        {
            database.Receipts.Add(SampleReceipt.Record(integrationEvent.ShipmentId, context.EventId.Value, integrationEvent.Parcels, nameof(ShipmentLabelPrinter)));
        }

        return outcome;
    }
}
