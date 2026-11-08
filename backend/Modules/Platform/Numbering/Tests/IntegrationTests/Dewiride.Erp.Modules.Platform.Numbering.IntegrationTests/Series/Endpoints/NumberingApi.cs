using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Documents;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.Numbering.IntegrationTests.Series.Endpoints;

internal static class NumberingApi
{
    public const string Route = "/api/platform/numbering";

    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static Uri DocumentsPath { get; } = new($"{Route}/documents", UriKind.Relative);

    public static Uri SeriesPath(Guid? id = null) => new(id is { } series ? $"{Route}/series/{series}" : $"{Route}/series", UriKind.Relative);

    public static Uri RouteFor(string template) => new(Route + template.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal), UriKind.Relative);

    public static object Format(string prefix, string year, string separator, int digits) => new { prefix, year, separator, digits };

    public static async Task<IReadOnlyList<NumberedDocumentResponse>> ListAsync(HttpClient client)
    {
        using var response = await client.GetAsync(DocumentsPath, TestContext.Current.CancellationToken);

        return await ReadAsync<List<NumberedDocumentResponse>>(response, HttpStatusCode.OK);
    }

    public static async Task<NumberedDocumentResponse> DocumentAsync(HttpClient client, NumberedDocument document) =>
        Assert.Single(await ListAsync(client), listed => listed.DocumentType == document.Key);

    public static Task<HttpResponseMessage> SetUpAsync(HttpClient client, NumberedDocument document, object format, long nextNumber, string? idempotencyKey = null) =>
        SendAsync(client, HttpMethod.Post, SeriesPath(), new { documentType = document.Key, format, nextNumber }, idempotencyKey ?? Guid.CreateVersion7().ToString());

    public static Task<HttpResponseMessage> ChangeAsync(HttpClient client, Guid id, object format, long? nextNumber, string version) =>
        SendAsync(client, HttpMethod.Put, SeriesPath(id), new { format, nextNumber, version }, idempotencyKey: null);

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<T>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(body);

        return body;
    }

    public static async Task<ValidationProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem.Status);
        Assert.Equal($"/problems/{code}", problem.Type);
        Assert.Equal(code, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());

        return problem;
    }

    // The tests draw the rule from the parameter store, so that no statutory value is written in them.
    public static async Task<GstDocumentSerialNumber> RuleOfAsync(IServiceProvider services, NumberedDocument document, FinancialYear year)
    {
        await using var scope = services.CreateAsyncScope();
        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<GstDocumentSerialNumbers>(year.Start, TestContext.Current.CancellationToken);

        return Assert.Single(set!.Rules, rule => rule.Rule == document.SerialNumberRule);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, Uri path, object body, string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add(IdempotencyKeyHeader, idempotencyKey);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
