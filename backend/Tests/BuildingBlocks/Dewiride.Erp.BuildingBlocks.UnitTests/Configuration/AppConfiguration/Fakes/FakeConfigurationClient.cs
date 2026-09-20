using Azure;
using Azure.Data.AppConfiguration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration.Fakes;

internal sealed class FakeConfigurationClient : ConfigurationClient
{
    private readonly List<ConfigurationSetting> _settings = [];

    public List<(string KeyFilter, string LabelFilter)> Selections { get; } = [];

    public List<(string Key, string? Label)> WatchedKeys { get; } = [];

    public FakeConfigurationClient With(string key, string value, string? label = null)
    {
        _settings.Add(new ConfigurationSetting(key, value, label, new ETag($"\"{Guid.CreateVersion7():N}\"")));

        return this;
    }

    public override AsyncPageable<ConfigurationSetting> GetConfigurationSettingsAsync(SettingSelector selector, CancellationToken cancellationToken = default)
    {
        Selections.Add((selector.KeyFilter, selector.LabelFilter));
        var page = Page<ConfigurationSetting>.FromValues(
            [.. _settings.Where(setting => Matches(selector.KeyFilter, setting.Key) && Matches(selector.LabelFilter, setting.Label))],
            continuationToken: null,
            new FakeResponse());

        return AsyncPageable<ConfigurationSetting>.FromPages([page]);
    }

    public override Task<Response<ConfigurationSetting>> GetConfigurationSettingAsync(string key, string? label = null, CancellationToken cancellationToken = default)
    {
        WatchedKeys.Add((key, label));
        var setting = _settings.SingleOrDefault(setting => setting.Key == key && setting.Label == label)
            ?? throw new RequestFailedException(404, $"{key} is not in the fake store.");

        return Task.FromResult(Response.FromValue(setting, new FakeResponse()));
    }

    private static bool Matches(string filter, string? value) => filter switch
    {
        LabelFilter.Null => value is null,
        KeyFilter.Any => true,
        _ when filter.EndsWith('*') => value is not null && value.StartsWith(filter[..^1], StringComparison.Ordinal),
        _ => string.Equals(filter, value, StringComparison.Ordinal),
    };
}
