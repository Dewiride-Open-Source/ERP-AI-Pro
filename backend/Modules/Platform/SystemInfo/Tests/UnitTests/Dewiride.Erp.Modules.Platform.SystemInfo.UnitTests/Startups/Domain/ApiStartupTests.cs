using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.UnitTests.Startups.Domain;

public sealed class ApiStartupTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 22, 4, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset RecordedAt = StartedAt.AddSeconds(2);

    [Fact]
    public void Record_ValidValues_CreatesAVersion7IdAndKeepsEveryValue()
    {
        var result = ApiStartup.Record("ERP-AI-Pro", new BuildInfo("1.2.3+abc", ".NET 10.0.1"), "Development", "local-dev", "DEV-BOX", StartedAt, RecordedAt);

        Assert.True(result.IsSuccess);
        var startup = result.Value;
        Assert.Equal(7, startup.Id.Value.Version);
        Assert.Equal("ERP-AI-Pro", startup.ApplicationName);
        Assert.Equal(new BuildInfo("1.2.3+abc", ".NET 10.0.1"), startup.Build);
        Assert.Equal("Development", startup.EnvironmentName);
        Assert.Equal("local-dev", startup.ConfigurationLabel);
        Assert.Equal("DEV-BOX", startup.MachineName);
        Assert.Equal(StartedAt, startup.StartedAt);
        Assert.Equal(RecordedAt, startup.RecordedAt);
    }

    [Fact]
    public void Record_NoConfigurationLabel_KeepsItNull()
    {
        var result = ApiStartup.Record("ERP-AI-Pro", new BuildInfo("1.0.0", ".NET 10.0.1"), "Production", null, "SERVER", StartedAt, RecordedAt);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ConfigurationLabel);
    }

    [Theory]
    [InlineData("", "1.0.0", ".NET 10.0.1", "Development", "DEV-BOX", "startup.application-name-required")]
    [InlineData("ERP-AI-Pro", " ", ".NET 10.0.1", "Development", "DEV-BOX", "startup.version-required")]
    [InlineData("ERP-AI-Pro", "1.0.0", "", "Development", "DEV-BOX", "startup.framework-required")]
    [InlineData("ERP-AI-Pro", "1.0.0", ".NET 10.0.1", "", "DEV-BOX", "startup.environment-name-required")]
    [InlineData("ERP-AI-Pro", "1.0.0", ".NET 10.0.1", "Development", "", "startup.machine-name-required")]
    public void Record_BlankValue_FailsWithTheMatchingCode(string applicationName, string version, string framework, string environmentName, string machineName, string expectedCode)
    {
        var result = ApiStartup.Record(applicationName, new BuildInfo(version, framework), environmentName, null, machineName, StartedAt, RecordedAt);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public void Record_RecordedBeforeStart_FailsWithRecordedBeforeStart()
    {
        var result = ApiStartup.Record("ERP-AI-Pro", new BuildInfo("1.0.0", ".NET 10.0.1"), "Development", null, "DEV-BOX", StartedAt, StartedAt.AddTicks(-1));

        Assert.True(result.IsFailure);
        Assert.Equal(StartupErrors.RecordedBeforeStart, result.Error);
    }

    [Fact]
    public void Record_ValuesLongerThanTheColumns_AreTruncatedToTheColumnLengths()
    {
        var longValue = new string('x', 500);

        var result = ApiStartup.Record(longValue, new BuildInfo(longValue, longValue), longValue, longValue, longValue, StartedAt, RecordedAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApiStartup.ApplicationNameMaxLength, result.Value.ApplicationName.Length);
        Assert.Equal(ApiStartup.VersionMaxLength, result.Value.Build.Version.Length);
        Assert.Equal(ApiStartup.FrameworkMaxLength, result.Value.Build.Framework.Length);
        Assert.Equal(ApiStartup.EnvironmentNameMaxLength, result.Value.EnvironmentName.Length);
        Assert.Equal(ApiStartup.ConfigurationLabelMaxLength, result.Value.ConfigurationLabel!.Length);
        Assert.Equal(ApiStartup.MachineNameMaxLength, result.Value.MachineName.Length);
    }
}
