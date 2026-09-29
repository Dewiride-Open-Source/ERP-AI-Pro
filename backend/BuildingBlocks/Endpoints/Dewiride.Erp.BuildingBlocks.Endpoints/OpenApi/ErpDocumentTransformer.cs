using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.OpenApi;

internal sealed class ErpDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info ??= new OpenApiInfo();
        document.Info.Title = ErpOpenApiOptions.Title;
        document.Info.Version = ErpOpenApiOptions.Version;
        document.Info.Description = "Modular ERP for Dewiride. Routes are grouped as /api/<domain>/<module>/<resource>, every response carries an X-Correlation-ID header and every error is an RFC 9457 problem document.";
        document.Info.License = new OpenApiLicense
        {
            Name = "GNU Lesser General Public License v3.0 only",
            Identifier = "LGPL-3.0-only",
        };

        // The browser calls /api on its own origin, so a server entry would name a host no caller should use.
        document.Servers?.Clear();

        return Task.CompletedTask;
    }
}
