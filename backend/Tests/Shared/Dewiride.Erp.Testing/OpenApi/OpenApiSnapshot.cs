using System.Text.Encodings.Web;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Endpoints.OpenApi;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Dewiride.Erp.Testing.OpenApi;

public static class OpenApiSnapshot
{
    public const string UpdateVariable = "ERP_OPENAPI_SNAPSHOT";

    public const string UpdateValue = "update";

    public const string RefreshCommand = $"cd backend && {UpdateVariable}={UpdateValue} dotnet test --project Tests/Host/Dewiride.Erp.Host.Api.IntegrationTests/Dewiride.Erp.Host.Api.IntegrationTests.csproj --filter-class \"*OpenApiSnapshotTests\"";

    private static readonly JsonSerializerOptions Formatting = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Path { get; } = RepositoryPaths.Combine("docs", "openapi", $"{ErpOpenApiOptions.DocumentName}.json");

    public static bool UpdateRequested =>
        string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), UpdateValue, StringComparison.OrdinalIgnoreCase);

    public static async Task<string> SerializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var provider = services.GetRequiredKeyedService<IOpenApiDocumentProvider>(ErpOpenApiOptions.DocumentName);
        var document = await provider.GetOpenApiDocumentAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await document.SerializeAsJsonAsync(buffer, OpenApiSpecVersion.OpenApi3_1, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        using var parsed = await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken).ConfigureAwait(false);

        return Format(parsed.RootElement);
    }

    public static async Task WriteAsync(string document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        await File.WriteAllTextAsync(Path, document, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        var committed = await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false);

        return committed.ReplaceLineEndings("\n");
    }

    public static string Describe(string committed, string current)
    {
        ArgumentNullException.ThrowIfNull(committed);
        ArgumentNullException.ThrowIfNull(current);

        var index = 0;
        while (index < committed.Length && index < current.Length && committed[index] == current[index])
        {
            index++;
        }

        return $"The committed OpenAPI document differs from the running API at index {index}.{System.Environment.NewLine}"
            + $"committed: …{Window(committed, index)}…{System.Environment.NewLine}"
            + $"running:   …{Window(current, index)}…{System.Environment.NewLine}"
            + $"Refresh it with: {RefreshCommand}";
    }

    private static string Format(JsonElement document) =>
        JsonSerializer.Serialize(document, Formatting).ReplaceLineEndings("\n") + "\n";

    private static string Window(string value, int index)
    {
        var start = Math.Max(0, index - 60);
        var length = Math.Min(160, value.Length - start);

        return length <= 0 ? string.Empty : value[start..(start + length)].ReplaceLineEndings(" ");
    }
}
