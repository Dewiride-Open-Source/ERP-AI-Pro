using System.Text.Json;
using System.Text.RegularExpressions;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.OpenApi;

namespace Dewiride.Erp.Host.Api.IntegrationTests.OpenApi;

public sealed partial class OpenApiSchemaShapeTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static readonly string[] UnsupportedKeywords = ["oneOf", "anyOf", "allOf", "not", "discriminator", "patternProperties", "if", "then", "else"];

    private static readonly string[] NameMaps = ["properties", "schemas", "paths", "responses", "content", "headers", "examples", "securitySchemes"];

    [Fact]
    public async Task Document_Always_AvoidsTheConstructsTheTypeScriptGeneratorCannotExpress()
    {
        var document = await ParseAsync();
        var violations = new List<string>();

        Inspect(document.RootElement, string.Empty, keysAreNames: false, violations);

        Assert.Empty(violations);
    }

    [Fact]
    public async Task Document_Always_NamesEverySchemaWithAPascalCaseIdentifier()
    {
        var document = await ParseAsync();

        var offenders = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .EnumerateObject()
            .Select(schema => schema.Name)
            .Where(name => !SchemaId().IsMatch(name))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public async Task Document_Always_DescribesEveryProblemResponseAsProblemJson()
    {
        var document = await ParseAsync();
        var offenders = new List<string>();

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                foreach (var response in operation.Value.GetProperty("responses").EnumerateObject().Where(r => !r.Name.StartsWith('2')))
                {
                    if (!response.Value.TryGetProperty("content", out var content) || !content.TryGetProperty("application/problem+json", out _))
                    {
                        offenders.Add($"{path.Name} {operation.Name} {response.Name}");
                    }
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Inspect_PropertyNamedLikeAMapKeyword_StillReportsItsComposedSchema()
    {
        using var document = JsonDocument.Parse("""
            {
              "components": {
                "schemas": {
                  "DocumentResponse": {
                    "type": "object",
                    "properties": {
                      "content": { "oneOf": [{ "type": "null" }, { "$ref": "#/components/schemas/Line" }] },
                      "oneOf": { "type": "string" }
                    }
                  }
                }
              }
            }
            """);
        var violations = new List<string>();

        Inspect(document.RootElement, string.Empty, keysAreNames: false, violations);

        Assert.Equal(["/components/schemas/DocumentResponse/properties/content/oneOf"], violations);
    }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex SchemaId();

    private static void Inspect(JsonElement element, string path, bool keysAreNames, List<string> violations)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var member in element.EnumerateObject())
                {
                    if (!keysAreNames && UnsupportedKeywords.Contains(member.Name, StringComparer.Ordinal))
                    {
                        violations.Add($"{path}/{member.Name}");
                    }

                    // Every value inside a map of names is a keyword object again, even when the name itself reads like a map keyword.
                    Inspect(member.Value, $"{path}/{member.Name}", !keysAreNames && NameMaps.Contains(member.Name, StringComparer.Ordinal), violations);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Inspect(item, $"{path}[{index++}]", keysAreNames: false, violations);
                }

                break;
            default:
                break;
        }
    }

    private async Task<JsonDocument> ParseAsync() =>
        JsonDocument.Parse(await OpenApiSnapshot.SerializeAsync(factory.Services, TestContext.Current.CancellationToken));
}
