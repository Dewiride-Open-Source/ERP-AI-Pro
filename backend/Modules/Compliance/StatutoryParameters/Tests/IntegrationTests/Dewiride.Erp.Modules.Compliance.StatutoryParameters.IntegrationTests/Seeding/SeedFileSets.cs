using System.Globalization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;

// The seed files as the seeder reads them, the expectations of the tests that hold the database and the API to the files. They are
// read with every two-digit GST State code in force on every day, because the seeding of the test database already checked the
// files' State codes against the reference data.
internal static class SeedFileSets
{
    private static readonly IReadOnlyList<IndianStateEntry> AnyStateCode =
        [.. Enumerable.Range(0, 100).Select(code => new IndianStateEntry(code.ToString("00", CultureInfo.InvariantCulture), "Any territory", IndianStateKind.State, DateOnly.MinValue, null))];

    public static IReadOnlyList<SeededSet> All { get; } = StatutorySeedFiles.Read(StatutorySeedFiles.Embedded(), AnyStateCode);

    public static SeededSet Of(string key) => All.Single(set => set.Set.Key == key);
}
