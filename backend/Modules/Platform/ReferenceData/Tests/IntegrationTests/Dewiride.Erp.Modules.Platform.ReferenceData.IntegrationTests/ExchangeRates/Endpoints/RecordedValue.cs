using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ExchangeRates.Endpoints;

internal sealed record RecordedValue(string Property, ChangeValueKind Kind, string? Before, string? After);
