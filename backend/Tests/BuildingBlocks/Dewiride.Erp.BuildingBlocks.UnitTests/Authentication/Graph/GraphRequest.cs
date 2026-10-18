namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Graph;

internal sealed record GraphRequest(Uri Address, string? ConsistencyLevel, string? Authorization);
