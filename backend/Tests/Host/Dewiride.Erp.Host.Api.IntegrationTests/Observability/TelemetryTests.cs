using System.Collections;
using System.Diagnostics;
using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.Testing;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Observability;

public sealed class TelemetryTests
{
    private const string StartupsPath = "/api/platform/system-info/startups";

    private const string SqlCommandLogCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    private static readonly TimeSpan ExportWait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Get_Startups_ExportsTheRequestSpanWithItsSqlSpanInOneTrace()
    {
        var spans = new ExportedItems<Activity>();
        await using var factory = new ErpApiFactory();
        using var traced = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(spans))));
        using var client = traced.CreateClient();

        using var response = await client.GetAsync(new Uri(StartupsPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = WaitFor(spans, span => span.Kind == ActivityKind.Server && Equals(span.GetTagItem("url.path"), StartupsPath));
        var sql = WaitFor(spans, span => span.Kind == ActivityKind.Client && span.TraceId == request.TraceId && span.GetTagItem("db.system.name") is not null);
        Assert.Contains("[platform_system_info].[Startups]", sql.GetTagItem("db.query.text") as string, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_WithACorrelationId_ExportsTheSqlCommandLogWithTheCorrelationIdAndTheRequestTraceId()
    {
        const string correlationId = "telemetry-4711";
        var spans = new ExportedItems<Activity>();
        var logs = new ExportedItems<LogRecord>();
        await using var factory = new ErpApiFactory();
        using var traced = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(spans));
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(logs));
        }));
        using var client = traced.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, StartupsPath);
        request.Headers.Add(CorrelationId.HeaderName, correlationId);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var server = WaitFor(spans, span => span.Kind == ActivityKind.Server && Equals(span.GetTagItem("url.path"), StartupsPath));
        var log = WaitFor(logs, record => record.TraceId == server.TraceId && record.CategoryName == SqlCommandLogCategory);
        Assert.Equal(correlationId, CorrelationIdOf(log));
    }

    private static T WaitFor<T>(ExportedItems<T> items, Func<T, bool> match)
        where T : class
    {
        T? found = null;
        Assert.True(SpinWait.SpinUntil(() => (found = items.FirstOrDefault(match)) is not null, ExportWait), $"No exported {typeof(T).Name} matched within {ExportWait}.");

        return found!;
    }

    private static string? CorrelationIdOf(LogRecord record)
    {
        string? value = null;
        record.ForEachScope((scope, _) =>
        {
            foreach (var item in scope)
            {
                if (item.Key == CorrelationIdScope.PropertyName)
                {
                    value = item.Value?.ToString();
                }
            }
        }, (object?)null);

        return value;
    }

    private sealed class ExportedItems<T> : ICollection<T>
    {
        private readonly List<T> _items = [];

        private readonly Lock _gate = new();

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _items.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            lock (_gate)
            {
                _items.Add(item);
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _items.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (_gate)
            {
                return _items.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (_gate)
            {
                _items.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(T item)
        {
            lock (_gate)
            {
                return _items.Remove(item);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (_gate)
            {
                return _items.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
