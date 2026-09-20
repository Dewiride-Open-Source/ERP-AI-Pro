namespace Dewiride.Erp.BuildingBlocks.Configuration.Sources;

public sealed record ErpConfigurationInfo(ErpConfigurationSource Source, string? Label, Uri? Endpoint);
