namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Sql;

internal sealed record TableRights(bool Insert, bool Update, bool Delete, bool Alter);
