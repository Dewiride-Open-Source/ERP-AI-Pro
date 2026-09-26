using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Persistence.Telemetry;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Telemetry;

public sealed class DatabaseTelemetryTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private static readonly ActivitySource Source = new("Dewiride.Erp.Tests.DatabaseTelemetry");

    [Fact]
    public async Task ExecuteScalar_CommandWithAParameter_ExportsAClientSpanWhoseQueryTextCarriesNoParameterValue()
    {
        const string secret = "ABCDE1234F";
        var spans = new List<Activity>();
        await using var provider = BuildProvider(spans, []);
        var tracer = provider.GetRequiredService<TracerProvider>();
        ActivitySpanId parentSpanId;

        using (var parent = Source.StartActivity("database-telemetry"))
        {
            Assert.NotNull(parent);
            parentSpanId = parent.SpanId;
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new SqlCommand("SELECT @value", connection);
            command.Parameters.AddWithValue("@value", secret);
            Assert.Equal(secret, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }

        tracer.ForceFlush();

        var span = Assert.Single(spans, candidate => candidate.ParentSpanId == parentSpanId);
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.Equal("SELECT @value", span.GetTagItem("db.query.text"));
        Assert.Equal("microsoft.sql_server", span.GetTagItem("db.system.name"));
        Assert.DoesNotContain(span.TagObjects, tag => tag.Key.StartsWith("db.query.parameter", StringComparison.Ordinal));
        Assert.DoesNotContain(span.TagObjects, tag => tag.Value?.ToString()?.Contains(secret, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Query_ThroughEntityFramework_ExportsTheOperationDurationAndTheEntityFrameworkMetrics()
    {
        var metrics = new List<Metric>();
        await using var provider = BuildProvider([], metrics);
        var meters = provider.GetRequiredService<MeterProvider>();

        await using (var scope = database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Samples.CountAsync(TestContext.Current.CancellationToken);
        }

        meters.ForceFlush();

        Assert.Contains(metrics, metric => metric.Name == "db.client.operation.duration");
        Assert.Contains(metrics, metric => metric.Name == "microsoft.entityframeworkcore.queries");
    }

    private static ServiceProvider BuildProvider(List<Activity> spans, List<Metric> metrics)
    {
        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(Source.Name).AddInMemoryExporter(spans))
            .WithMetrics(meters => meters.AddInMemoryExporter(metrics));
        services.AddErpDatabaseTelemetry();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TracerProvider>();
        provider.GetRequiredService<MeterProvider>();

        return provider;
    }
}
