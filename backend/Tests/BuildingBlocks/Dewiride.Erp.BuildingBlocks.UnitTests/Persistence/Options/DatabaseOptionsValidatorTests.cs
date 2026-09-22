using Dewiride.Erp.BuildingBlocks.Persistence.Options;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Options;

public sealed class DatabaseOptionsValidatorTests
{
    private readonly DatabaseOptionsValidator _validator = new();

    [Fact]
    public void Validate_ConnectionStringMissing_FailsNamingTheKeyAndEverySource()
    {
        var result = _validator.Validate(null, new DatabaseOptions());

        Assert.True(result.Failed);
        Assert.Contains($"{DatabaseOptions.SectionName}:ConnectionString", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Erp--Platform--Database--ConnectionString", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("user-secrets", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Erp__Platform__Database__ConnectionString", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("/run/secrets/Erp__Platform__Database__ConnectionString", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ConnectionStringPresent_Succeeds()
    {
        var result = _validator.Validate(null, new DatabaseOptions { ConnectionString = "Server=localhost;Database=ErpAiPro;Integrated Security=True" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Defaults_MatchTheDocumentedValues()
    {
        var options = new DatabaseOptions();

        Assert.Null(options.ConnectionString);
        Assert.Equal(DatabaseProvider.SqlServer, options.Provider);
        Assert.Equal(TimeSpan.FromSeconds(30), options.CommandTimeout);
        Assert.Equal(5, options.MaxRetryCount);
        Assert.Equal(TimeSpan.FromSeconds(10), options.MaxRetryDelay);
    }
}
