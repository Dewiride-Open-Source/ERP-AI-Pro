namespace Dewiride.Erp.Host.Migrator.IntegrationTests;

public sealed class RunRecorder
{
    private readonly List<string> _runs = [];

    public IReadOnlyList<string> Runs => _runs;

    public void Record(string entry) => _runs.Add(entry);
}
