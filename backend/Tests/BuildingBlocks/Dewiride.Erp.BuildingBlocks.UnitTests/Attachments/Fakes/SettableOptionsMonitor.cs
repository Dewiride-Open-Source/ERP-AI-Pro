using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Fakes;

internal sealed class SettableOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
{
    public TOptions CurrentValue { get; set; } = value;

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
