using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.Idempotency.Http;

public static class IdempotencyEndpointExtensions
{
    public static TBuilder RequireIdempotencyKey<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithMetadata(RequireIdempotencyKeyMetadata.Instance);
        builder.ProducesProblem(StatusCodes.Status400BadRequest);
        builder.ProducesProblem(StatusCodes.Status409Conflict);
        builder.ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = IdempotencyKeyHeader.Name,
                In = ParameterLocation.Header,
                Required = true,
                Description = "A UUID chosen by the client for this request; repeating the request with the same key replays the first response instead of acting twice.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
            });

            return Task.CompletedTask;
        });

        return builder;
    }
}
