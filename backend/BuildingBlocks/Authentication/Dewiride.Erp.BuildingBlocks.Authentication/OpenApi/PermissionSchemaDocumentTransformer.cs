using System.Text.Json.Nodes;
using Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Responses;
using Dewiride.Erp.BuildingBlocks.Modules;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.Authentication.OpenApi;

// The permissions a person holds are names of the catalogue, so the document lists every name in the schema Permission, and
// the client generated from it gives the web app a type that refuses a name no module declares.
internal sealed class PermissionSchemaDocumentTransformer(PermissionCatalog catalog) : IOpenApiDocumentTransformer
{
    public const string SchemaId = "Permission";

    public const string PermissionsProperty = "permissions";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Components?.Schemas is not { } schemas
            || !schemas.TryGetValue(nameof(CurrentUserResponse), out var personSchema)
            || personSchema is not OpenApiSchema { Properties: { } properties }
            || !properties.TryGetValue(PermissionsProperty, out var permissionsSchema)
            || permissionsSchema is not OpenApiSchema permissions)
        {
            throw new InvalidOperationException($"The OpenAPI document describes no {nameof(CurrentUserResponse)} with {PermissionsProperty}.");
        }

        if (!document.AddComponent<IOpenApiSchema>(SchemaId, new OpenApiSchema { Enum = [.. catalog.Names.Select(name => (JsonNode)JsonValue.Create(name))] }))
        {
            throw new InvalidOperationException($"The OpenAPI document already declares the schema '{SchemaId}'.");
        }

        permissions.Items = new OpenApiSchemaReference(SchemaId, document);

        return Task.CompletedTask;
    }
}
