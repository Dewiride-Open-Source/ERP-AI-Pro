using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.OpenApi;

public static class ErpOpenApiOptions
{
    public const string DocumentName = "erp";

    public const string Title = "ERP-AI-Pro API";

    public const string Version = "1.0";

    public static void Configure(OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
        var referenceIds = new SchemaReferenceIds(options.CreateSchemaReferenceId);
        options.CreateSchemaReferenceId = referenceIds.Create;
        options.AddDocumentTransformer<ErpDocumentTransformer>();
        options.AddSchemaTransformer<ProblemDetailsSchemaTransformer>();
    }
}
