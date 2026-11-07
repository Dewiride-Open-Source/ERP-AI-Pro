namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.Seeding;

// The countries and State codes are one set of rows for every host of the test process, and the seeder tests add and rename
// rows that the list tests count and name, so the tests that read or change those rows never run beside each other.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ReferenceRowReaders
{
    public const string Name = "Reference row readers";
}
