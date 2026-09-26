namespace Dewiride.Erp.Host.Migrator.IntegrationTests.Seeding;

public sealed class SeedRecorder
{
    private readonly List<string> _runs = [];

    public IReadOnlyList<string> Runs => _runs;

    public void Record(string entry) => _runs.Add(entry);
}
