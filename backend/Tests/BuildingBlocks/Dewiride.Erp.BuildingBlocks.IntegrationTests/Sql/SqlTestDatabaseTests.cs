using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Data.SqlClient;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Sql;

public sealed class SqlTestDatabaseTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveServerConnectionString_VariableMissing_ThrowsNamingTheVariableAndTheGuide(string? value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => SqlTestDatabase.ResolveServerConnectionString(_ => value));

        Assert.Contains(SqlTestDatabase.ConnectionVariable, exception.Message, StringComparison.Ordinal);
        Assert.Contains("docs/guides/testing.md", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Server=localhost;Database=ErpAiPro;Integrated Security=True")]
    [InlineData("Server=localhost;Initial Catalog=ErpAiPro;Integrated Security=True")]
    public void ResolveServerConnectionString_VariableNamesADatabase_ThrowsNamingTheDatabase(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => SqlTestDatabase.ResolveServerConnectionString(_ => value));

        Assert.Contains(SqlTestDatabase.ConnectionVariable, exception.Message, StringComparison.Ordinal);
        Assert.Contains("ErpAiPro", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveServerConnectionString_ServerLevelVariable_ReturnsItNormalised()
    {
        var resolved = SqlTestDatabase.ResolveServerConnectionString(name => name == SqlTestDatabase.ConnectionVariable ? "Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" : null);

        var builder = new SqlConnectionStringBuilder(resolved);
        Assert.Equal("localhost", builder.DataSource);
        Assert.Empty(builder.InitialCatalog);
        Assert.True(builder.IntegratedSecurity);
    }

    [Fact]
    public void Current_WhileTheFixtureIsAlive_NamesAPerProcessDatabaseAndTheFactoryUsesIt()
    {
        var database = SqlTestDatabase.Current;

        Assert.StartsWith(SqlTestDatabase.NamePrefix, database.Name, StringComparison.Ordinal);
        Assert.Equal(database.Name, new SqlConnectionStringBuilder(database.ConnectionString).InitialCatalog);
        using var factory = new ErpApiFactory();
        Assert.Equal(database.ConnectionString, factory.Services.GetRequiredService<IConfiguration>()[ErpApiFactory.DatabaseConnectionKey]);
    }
}
