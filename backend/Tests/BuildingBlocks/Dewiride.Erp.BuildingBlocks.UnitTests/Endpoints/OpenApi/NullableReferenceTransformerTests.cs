using System.Text.Json.Nodes;
using Dewiride.Erp.BuildingBlocks.Endpoints.OpenApi;
using Microsoft.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.OpenApi;

public sealed class NullableReferenceTransformerTests
{
    [Fact]
    public void Apply_NullablePropertyReferringToAnEnum_DescribesItByTheReferenceAloneAndTheEnumByItsMembers()
    {
        var document = Document();
        document.Components!.Schemas!["Reason"] = new OpenApiSchema { Enum = [JsonValue.Create("administrator"), JsonValue.Create("accountDisabled"), JsonNullSentinel.JsonNull] };
        var reference = new OpenApiSchemaReference("Reason", document) { Description = "Why the record was deactivated." };
        document.Components.Schemas["Person"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["reason"] = Nullable(reference),
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
            },
        };

        NullableReferenceTransformer.Apply(document);

        var person = Assert.IsType<OpenApiSchema>(document.Components.Schemas["Person"]);
        Assert.Same(reference, person.Properties!["reason"]);
        Assert.Equal(JsonSchemaType.String | JsonSchemaType.Null, person.Properties["name"].Type);
        var reason = Assert.IsType<OpenApiSchema>(document.Components.Schemas["Reason"]);
        Assert.Equal(["administrator", "accountDisabled"], reason.Enum!.Select(value => value.GetValue<string>()));
    }

    [Fact]
    public void Apply_NullableItemsAndBodies_DescribesThemByTheReferenceAlone()
    {
        var document = Document();
        document.Components!.Schemas!["Team"] = new OpenApiSchema { Type = JsonSchemaType.Object };
        var items = new OpenApiSchemaReference("Team", document);
        var body = new OpenApiSchemaReference("Team", document);
        var list = new OpenApiSchema { Type = JsonSchemaType.Array, Items = Nullable(items) };
        var response = new OpenApiMediaType { Schema = Nullable(body) };
        document.Components.Schemas["TeamList"] = list;
        document.Paths = new OpenApiPaths
        {
            ["/teams"] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = new OpenApiOperation
                    {
                        Responses = new OpenApiResponses
                        {
                            ["200"] = new OpenApiResponse { Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal) { ["application/json"] = response } },
                        },
                    },
                },
            },
        };

        NullableReferenceTransformer.Apply(document);

        Assert.Same(items, list.Items);
        Assert.Same(body, response.Schema);
    }

    [Fact]
    public void Apply_ChoiceBetweenTwoSchemas_LeavesItAsItIs()
    {
        var document = Document();
        document.Components!.Schemas!["Card"] = new OpenApiSchema { Type = JsonSchemaType.Object };
        document.Components.Schemas["Cash"] = new OpenApiSchema { Type = JsonSchemaType.Object };
        var choice = new OpenApiSchema { OneOf = [new OpenApiSchemaReference("Card", document), new OpenApiSchemaReference("Cash", document)] };
        document.Components.Schemas["Payment"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["method"] = choice },
        };

        NullableReferenceTransformer.Apply(document);

        Assert.Same(choice, Assert.IsType<OpenApiSchema>(document.Components.Schemas["Payment"]).Properties!["method"]);
    }

    private static OpenApiDocument Document() =>
        new() { Components = new OpenApiComponents { Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) } };

    private static OpenApiSchema Nullable(OpenApiSchemaReference reference) =>
        new() { OneOf = [new OpenApiSchema { Type = JsonSchemaType.Null }, reference] };
}
