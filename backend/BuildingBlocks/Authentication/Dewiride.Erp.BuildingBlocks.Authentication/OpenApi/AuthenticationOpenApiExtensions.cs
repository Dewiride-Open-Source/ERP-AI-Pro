using Microsoft.AspNetCore.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.Authentication.OpenApi;

public static class AuthenticationOpenApiExtensions
{
    public static OpenApiOptions AddErpSecurityDescription(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .AddOperationTransformer<SecurityRequirementsOperationTransformer>()
            .AddDocumentTransformer<SecuritySchemesDocumentTransformer>();
    }
}
