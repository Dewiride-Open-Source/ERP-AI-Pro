using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Persistence.Telemetry;
using Dewiride.Erp.Testing.Telemetry;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Telemetry;

public sealed class DatabaseTelemetryTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private const string Secret = "ABCDE1234F";

    private static readonly ActivitySource Source = new("Dewiride.Erp.Tests.DatabaseTelemetry");

    [Fact]
    public async Task ExecuteScalar_CommandWithAParameter_ExportsAClientSpanWhoseQueryTextCarriesNoParameterValue()
    {
        var spans = new ExportedItemCollection<ExportedSpan>();
        await using var provider = BuildProvider(spans, new ExportedItemCollection<Metric>());

        var parentSpanId = await RunUnderParentAsync(async connection =>
        {
            await using var command = new SqlCommand("SELECT @value", connection);
            command.Parameters.AddWithValue("@value", Secret);
            Assert.Equal(Secret, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        });

        var span = Assert.Single(spans, candidate => candidate.ParentSpanId == parentSpanId);
        Assert.Equal(SqlErrorStatusProcessor.SqlClientSourceName, span.SourceName);
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.Equal("SELECT @value", span.Tag("db.query.text"));
        Assert.Equal("microsoft.sql_server", span.Tag("db.system.name"));
        Assert.DoesNotContain(span.Tags, tag => tag.Key.StartsWith("db.query.parameter", StringComparison.Ordinal));
        Assert.False(span.Mentions(Secret));
    }

    [Fact]
    public async Task ExecuteNonQuery_DuplicateKeyTheServerQuotes_ExportsAnErrorSpanWithoutTheQuotedValue()
    {
        var spans = new ExportedItemCollection<ExportedSpan>();
        await using var provider = BuildProvider(spans, new ExportedItemCollection<Metric>());

        var parentSpanId = await RunUnderParentAsync(async connection =>
        {
            await using var command = new SqlCommand(
                "CREATE TABLE #numbers (value nvarchar(50) NOT NULL PRIMARY KEY); INSERT INTO #numbers VALUES (@value); INSERT INTO #numbers VALUES (@value);",
                connection);
            command.Parameters.AddWithValue("@value", Secret);
            var failure = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
            Assert.Contains(Secret, failure.Message, StringComparison.Ordinal);
        });

        var span = Assert.Single(spans, candidate => candidate.ParentSpanId == parentSpanId);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Equal("2627", span.Tag("db.response.status_code"));
        Assert.False(span.Mentions(Secret));
    }

    [Fact]
    public async Task Query_ThroughEntityFramework_ExportsTheOperationDurationAndTheEntityFrameworkMetrics()
    {
        var metrics = new ExportedItemCollection<Metric>();
        await using var provider = BuildProvider(new ExportedItemCollection<ExportedSpan>(), metrics);
        var meters = provider.GetRequiredService<MeterProvider>();

        await using (var scope = database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Samples.CountAsync(TestContext.Current.CancellationToken);
        }

        meters.ForceFlush();

        Assert.Contains(metrics, metric => metric.Name == "db.client.operation.duration");
        Assert.Contains(metrics, metric => metric.Name == "microsoft.entityframeworkcore.queries");
    }

    private async Task<ActivitySpanId> RunUnderParentAsync(Func<SqlConnection, Task> work)
    {
        using var parent = Source.StartActivity("database-telemetry");
        Assert.NotNull(parent);
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await work(connection);

        return parent.SpanId;
    }

    // The database telemetry is registered before the recorder, as the host registers it before its exporter, so the
    // recorder sees each span exactly as an exporter would.
    private static ServiceProvider BuildProvider(ExportedItemCollection<ExportedSpan> spans, ExportedItemCollection<Metric> metrics)
    {
        var services = new ServiceCollection();
        services.AddErpDatabaseTelemetry();
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(Source.Name).AddProcessor(new SpanRecorder(spans)))
            .WithMetrics(meters => meters.AddInMemoryExporter(metrics));
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TracerProvider>();
        provider.GetRequiredService<MeterProvider>();

        return provider;
    }

    private sealed class SpanRecorder(ExportedItemCollection<ExportedSpan> spans) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data) =>
            spans.Add(new ExportedSpan(data.ParentSpanId, data.Source.Name, data.Kind, data.Status, data.StatusDescription, [.. data.TagObjects]));
    }

    private sealed record ExportedSpan(
        ActivitySpanId ParentSpanId,
        string SourceName,
        ActivityKind Kind,
        ActivityStatusCode Status,
        string? StatusDescription,
        IReadOnlyList<KeyValuePair<string, object?>> Tags)
    {
        public object? Tag(string key) => Tags.FirstOrDefault(tag => tag.Key == key).Value;

        public bool Mentions(string value) =>
            StatusDescription?.Contains(value, StringComparison.Ordinal) == true
            || Tags.Any(tag => tag.Value?.ToString()?.Contains(value, StringComparison.Ordinal) == true);
    }
}
