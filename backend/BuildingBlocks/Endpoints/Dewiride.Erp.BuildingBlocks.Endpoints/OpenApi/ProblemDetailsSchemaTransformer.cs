using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.OpenApi;

internal sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    // Generated clients throw the deserialized problem and fill its message from the member marked with this extension.
    private const string PrimaryErrorMessage = "x-ms-primary-error-message";

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (!typeof(ProblemDetails).IsAssignableFrom(context.JsonTypeInfo.Type))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        schema.Properties[ResultExtensions.CodeExtension] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Stable machine-readable error code; the type member is this code under /problems/.",
        };
        schema.Properties[ProblemDetailsCustomizer.TraceIdExtension] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Correlation id of the request, the same value the X-Correlation-ID response header carries.",
        };

        if (schema.Properties.TryGetValue("title", out var title) && title is OpenApiSchema writable)
        {
            writable.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            writable.Extensions[PrimaryErrorMessage] = new JsonNodeExtension(true);
        }

        return Task.CompletedTask;
    }
}
