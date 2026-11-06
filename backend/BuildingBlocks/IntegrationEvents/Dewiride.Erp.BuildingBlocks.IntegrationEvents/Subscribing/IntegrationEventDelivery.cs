using System.Text.Json;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;

// The payload of a delivery run, carrying the event itself, so a run re-run after its outbox row was deleted still has its event.
// Its shape is stored in every pending delivery, so a change to it ships under new subscriber job names.
internal sealed record IntegrationEventDelivery(Guid EventId, string EventName, string Source, DateTimeOffset OccurredAt, string? CorrelationId, JsonElement Event);
