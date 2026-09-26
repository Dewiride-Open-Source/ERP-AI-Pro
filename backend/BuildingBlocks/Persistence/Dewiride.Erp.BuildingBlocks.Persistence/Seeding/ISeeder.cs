namespace Dewiride.Erp.BuildingBlocks.Persistence.Seeding;

// A seeder runs on every migrator run and in every test database, so it must leave the same rows however often it
// runs: insert what is missing by its natural key and never duplicate or overwrite what a person changed.
public interface ISeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}
