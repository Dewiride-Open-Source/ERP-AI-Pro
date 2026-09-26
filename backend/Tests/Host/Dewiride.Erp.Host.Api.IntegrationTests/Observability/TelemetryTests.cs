using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Telemetry;
using Microsoft.AspNetCore.WebUtilities;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Observability;

public sealed class TelemetryTests
{
    private const string StartupsPath = "/api/platform/system-info/startups";

    private const string AttachmentsPath = "/api/platform/attachments";

    private const string BlobSourcePrefix = "Azure.Storage.Blobs.";

    private const string SqlCommandLogCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    private static readonly TimeSpan ExportWait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Get_Startups_ExportsTheRequestSpanWithItsSqlSpanInOneTrace()
    {
        var spans = new ExportedItemCollection<Activity>();
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
        var spans = new ExportedItemCollection<Activity>();
        var logs = new ExportedItemCollection<LogRecord>();
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

    [Fact]
    public async Task Post_Attachment_ExportsTheBlobStorageSpansInTheUploadTrace()
    {
        var spans = new ExportedItemCollection<Activity>();
        await using var factory = new ErpApiFactory();
        using var traced = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(spans))));
        using var client = traced.CreateClient();

        using var response = await UploadAsync(client, UniqueText());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var traceId = TraceIdOf(response);
        WaitFor(spans, span => span.Kind == ActivityKind.Server && span.TraceId.ToHexString() == traceId);
        var blobSpans = spans.Where(span => span.TraceId.ToHexString() == traceId && span.Source.Name.StartsWith(BlobSourcePrefix, StringComparison.Ordinal)).ToList();
        Assert.Contains(blobSpans, span => span.DisplayName == "BlockBlobClient.StageBlock");
        Assert.Contains(blobSpans, span => span.DisplayName == "BlockBlobClient.CommitBlockList");
    }

    [Fact]
    public async Task Get_AttachmentContent_ExportsNoSpanCarryingTheLinkToken()
    {
        var spans = new ExportedItemCollection<Activity>();
        await using var factory = new ErpApiFactory();
        using var traced = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(spans))));
        using var client = traced.CreateClient();
        var text = UniqueText();
        using var upload = await UploadAsync(client, text);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
        using var link = await client.PostAsync(new Uri($"{AttachmentsPath}/{id}/download-links", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        var url = (await link.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("url").GetString()!;
        var token = QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, url).Query)["link"].ToString();
        Assert.False(string.IsNullOrEmpty(token));

        using var download = await client.GetAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(text, await download.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var traceId = TraceIdOf(download);
        var server = WaitFor(spans, span => span.Kind == ActivityKind.Server && span.TraceId.ToHexString() == traceId);
        Assert.NotNull(server.GetTagItem("url.query"));
        Assert.Contains(spans, span => span.Kind == ActivityKind.Client && span.TraceId.ToHexString() == traceId);
        Assert.DoesNotContain(spans, span => Carries(span, token));
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string text)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "telemetry.txt");

        return await client.PostAsync(new Uri(AttachmentsPath, UriKind.Relative), form, TestContext.Current.CancellationToken);
    }

    private static string UniqueText() => $"telemetry {Guid.CreateVersion7().ToString("N")[^12..]}";

    private static string TraceIdOf(HttpResponseMessage response) => Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));

    private static bool Carries(Activity span, string value) =>
        span.DisplayName.Contains(value, StringComparison.Ordinal)
        || (span.StatusDescription?.Contains(value, StringComparison.Ordinal) ?? false)
        || span.TagObjects.Any(tag => Mentions(tag.Value, value))
        || span.Events.Any(item => item.Name.Contains(value, StringComparison.Ordinal) || item.Tags.Any(tag => Mentions(tag.Value, value)))
        || span.Links.Any(item => item.Tags?.Any(tag => Mentions(tag.Value, value)) ?? false)
        || span.Baggage.Any(item => Mentions(item.Value, value));

    private static bool Mentions(object? tagValue, string value) =>
        tagValue switch
        {
            null => false,
            string text => text.Contains(value, StringComparison.Ordinal),
            IEnumerable items => items.Cast<object?>().Any(item => Mentions(item, value)),
            _ => Convert.ToString(tagValue, CultureInfo.InvariantCulture)?.Contains(value, StringComparison.Ordinal) ?? false,
        };

    private static T WaitFor<T>(ExportedItemCollection<T> items, Func<T, bool> match)
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
}
