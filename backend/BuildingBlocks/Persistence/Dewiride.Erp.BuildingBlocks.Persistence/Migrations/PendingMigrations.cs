namespace Dewiride.Erp.BuildingBlocks.Persistence.Migrations;

public sealed record PendingMigrations(string Schema, IReadOnlyList<string> Migrations);
