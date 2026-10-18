using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.OpenApi;

// A nullable property whose schema is a reference, to a record or an enum, is generated as oneOf a null schema and the
// reference, and the enum it refers to with null among its values. The TypeScript generator mishandles the first
// (OpenApiSchemaShapeTests) and turns the second into an enum member of its own, while every property it generates may hold
// null whatever the document says. So such a property is described by the reference alone, its description saying when it is
// null, and an enum by its members alone.
internal sealed class NullableReferenceTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        Apply(document);

        return Task.CompletedTask;
    }

    internal static void Apply(OpenApiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (var schema in document.Components?.Schemas?.Values ?? [])
        {
            Visit(schema);
        }

        foreach (var operation in document.Paths?.Values.SelectMany(OperationsOf) ?? [])
        {
            foreach (var media in ContentOf(operation))
            {
                if (media.Schema is { } schema)
                {
                    media.Schema = Collapse(schema);
                    Visit(media.Schema);
                }
            }
        }
    }

    private static IEnumerable<OpenApiOperation> OperationsOf(IOpenApiPathItem path) =>
        path.Operations is { } operations ? operations.Values : [];

    private static IEnumerable<OpenApiMediaType> ContentOf(OpenApiOperation operation)
    {
        IEnumerable<OpenApiMediaType> requests = operation.RequestBody?.Content is { } request ? request.Values : [];
        var responses = operation.Responses is { } answers
            ? answers.Values.SelectMany(response => response.Content is { } content ? content.Values : Enumerable.Empty<OpenApiMediaType>())
            : [];

        return requests.Concat(responses);
    }

    private static void Visit(IOpenApiSchema schema)
    {
        if (schema is not OpenApiSchema inline)
        {
            return;
        }

        if (inline.Enum is { } values)
        {
            for (var index = values.Count - 1; index >= 0; index--)
            {
                if (values[index] is null || JsonNullSentinel.IsJsonNullSentinel(values[index]))
                {
                    values.RemoveAt(index);
                }
            }
        }

        if (inline.Properties is { } properties)
        {
            foreach (var name in properties.Keys.ToList())
            {
                properties[name] = Collapse(properties[name]);
                Visit(properties[name]);
            }
        }

        if (inline.Items is { } items)
        {
            inline.Items = Collapse(items);
            Visit(inline.Items);
        }

        if (inline.AdditionalProperties is { } additional)
        {
            inline.AdditionalProperties = Collapse(additional);
            Visit(inline.AdditionalProperties);
        }
    }

    private static IOpenApiSchema Collapse(IOpenApiSchema schema) =>
        schema is OpenApiSchema { OneOf: [{ Type: JsonSchemaType.Null }, OpenApiSchemaReference reference] } ? reference : schema;
}
