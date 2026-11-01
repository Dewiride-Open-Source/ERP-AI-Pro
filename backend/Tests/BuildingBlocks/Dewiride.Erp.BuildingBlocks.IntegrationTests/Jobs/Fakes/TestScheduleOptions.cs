using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;

// The options a test's recurring job reads its schedule from. The test changes Expression between polls, as a refresh of the
// configuration store does, and both runners of the test read the same instance.
public sealed class TestScheduleOptions : IOptionsMonitor<TestScheduleOptions>
{
    public string Expression { get; set; } = "30 0 * * *";

    public bool Unreadable { get; set; }

    public TestScheduleOptions CurrentValue => Unreadable ? throw new OptionsValidationException(nameof(TestScheduleOptions), typeof(TestScheduleOptions), ["The schedule setting failed validation."]) : this;

    public TestScheduleOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<TestScheduleOptions, string?> listener) => null;
}
