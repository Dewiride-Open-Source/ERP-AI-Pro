using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;

// The seeded sets are one set of rows for every host of the test process, and the seeder tests change, delete and add rows
// that the other tests read, so every test that reads or changes them runs in this collection, one at a time, on one host.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StatutoryRows : ICollectionFixture<ErpApiFactory>
{
    public const string Name = "Statutory rows";
}
