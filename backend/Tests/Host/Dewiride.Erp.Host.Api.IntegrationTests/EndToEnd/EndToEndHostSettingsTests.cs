using Dewiride.Erp.Testing.EndToEnd;

namespace Dewiride.Erp.Host.Api.IntegrationTests.EndToEnd;

public sealed class EndToEndHostSettingsTests
{
    private static readonly string[] Arguments = ["--port", "5180", "--web-origin", "https://localhost:3200"];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Read_EnvironmentOtherThanTesting_ThrowsNamingTheVariableAndTheEnvironment(string? environment)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EndToEndHostSettings.Read(Arguments, EnvironmentNamed(environment)));

        Assert.Contains(EndToEndHostSettings.EnvironmentVariable, exception.Message, StringComparison.Ordinal);
        Assert.Contains(EndToEndHost.EnvironmentName, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("testing")]
    public void Read_TestingWithAPortAndAWebOrigin_ReturnsThem(string environment)
    {
        var settings = EndToEndHostSettings.Read(Arguments, EnvironmentNamed(environment));

        Assert.Equal(new EndToEndHostSettings(5180, "https://localhost:3200"), settings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("5180x")]
    public void Read_PortMissingOrOutOfRange_ThrowsNamingTheArgument(string? port)
    {
        string[] args = port is null ? ["--web-origin", "https://localhost:3200"] : ["--port", port, "--web-origin", "https://localhost:3200"];

        var exception = Assert.Throws<InvalidOperationException>(() => EndToEndHostSettings.Read(args, EnvironmentNamed(EndToEndHost.EnvironmentName)));

        Assert.Contains($"--{EndToEndHostSettings.PortArgument}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_WebOriginMissing_ThrowsNamingTheArgument()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EndToEndHostSettings.Read(["--port", "5180"], EnvironmentNamed(EndToEndHost.EnvironmentName)));

        Assert.Contains($"--{EndToEndHostSettings.WebOriginArgument}", exception.Message, StringComparison.Ordinal);
    }

    private static Func<string, string?> EnvironmentNamed(string? environment) =>
        name => name == EndToEndHostSettings.EnvironmentVariable ? environment : null;
}
