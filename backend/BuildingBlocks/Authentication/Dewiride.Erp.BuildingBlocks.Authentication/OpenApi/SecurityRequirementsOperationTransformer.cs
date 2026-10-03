using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.Authentication.OpenApi;

// The requirements follow the routing of RouteSignInScheme and the rule of AntiforgeryValidationMiddleware from the
// endpoint's metadata alone, never from the registered schemes or the bearer flag, because the test host that writes the
// committed document registers its own default scheme. A requirement whose reference has no host document is written as
// {}, which declares anonymous access, so every reference names the document being built.
internal sealed class SecurityRequirementsOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var document = context.Document
            ?? throw new InvalidOperationException($"No OpenAPI document was given for {context.Description.HttpMethod} {context.Description.RelativePath}.");
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var bearer = metadata.OfType<BearerTokenRouteMetadata>().LastOrDefault();
        var checksAntiforgery = bearer is null && ChecksAntiforgery(context.Description.HttpMethod, metadata);

        operation.Security = (metadata.OfType<IAllowAnonymous>().Any(), bearer) switch
        {
            // An anonymous request passes, and one the session cookie signs in is still checked for its antiforgery token.
            (true, _) => checksAntiforgery ? [new OpenApiSecurityRequirement(), Session(document, checksAntiforgery)] : [],
            (false, { } access) => [.. access.Access.Scopes.Concat(access.Access.ApplicationRoles).Select(name => BearerToken(document, name))],
            (false, null) => [Session(document, checksAntiforgery)],
        };

        return Task.CompletedTask;
    }

    private static OpenApiSecurityRequirement Session(OpenApiDocument document, bool withAntiforgeryToken)
    {
        var requirement = new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SecuritySchemeIds.SessionCookie, document)] = [] };
        if (withAntiforgeryToken)
        {
            requirement[new OpenApiSecuritySchemeReference(SecuritySchemeIds.AntiforgeryToken, document)] = [];
        }

        return requirement;
    }

    // Each alternative names one scope or application role, any one of which grants the route.
    private static OpenApiSecurityRequirement BearerToken(OpenApiDocument document, string name) =>
        new() { [new OpenApiSecuritySchemeReference(SecuritySchemeIds.BearerToken, document)] = [name] };

    private static bool ChecksAntiforgery(string? method, IList<object> metadata) =>
        method is not null
        && (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
        && metadata.OfType<IAntiforgeryMetadata>().LastOrDefault() is not { RequiresValidation: false };
}
