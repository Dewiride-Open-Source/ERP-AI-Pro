using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.Authentication.OpenApi;

// Runs after every operation transformer, so it declares exactly the schemes the operations name. AddComponent creates the
// components when absent, never replaces an id and registers the scheme in the document's workspace.
internal sealed class SecuritySchemesDocumentTransformer : IOpenApiDocumentTransformer
{
    public const string SessionCookieDescription =
        $"The session cookie {SessionCookieOptionsSetup.CookieName}, HttpOnly and Secure, which the API sets once a Microsoft Entra ID sign-in started at {AuthPaths.Login} completes; a browser sends it on every request to this origin.";

    public const string AntiforgeryTokenDescription =
        $"The value of the readable cookie {AntiforgeryTokens.RequestTokenCookieName}, required with the session cookie on every POST, PUT, PATCH and DELETE it signs in, which otherwise answer 400 {AntiforgeryProblems.TokenMissing} or {AntiforgeryProblems.TokenInvalid}; GET {AuthPaths.Antiforgery} issues it again, and POST {AuthPaths.Logout} also takes it in the form field {AntiforgeryTokens.FormFieldName}.";

    public const string BearerTokenDescription =
        $"A Microsoft Entra ID v2.0 access token issued in this tenant for this API's app registration, taken only by the operations that list it and only while the feature flag {BearerTokenFeature.Name} is on: a person's token needs one of the names an operation lists in its scp claim, an application's token one of them in its roles claim.";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        DeclareWhenNamed(document, SecuritySchemeIds.SessionCookie, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = SessionCookieOptionsSetup.CookieName,
            Description = SessionCookieDescription,
        });
        DeclareWhenNamed(document, SecuritySchemeIds.AntiforgeryToken, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = AntiforgeryTokens.HeaderName,
            Description = AntiforgeryTokenDescription,
        });
        DeclareWhenNamed(document, SecuritySchemeIds.BearerToken, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = BearerTokenDescription,
        });

        return Task.CompletedTask;
    }

    private static void DeclareWhenNamed(OpenApiDocument document, string id, OpenApiSecurityScheme scheme)
    {
        var named = document.Paths.Values
            .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            .SelectMany(operation => operation.Security ?? [])
            .Any(requirement => requirement.Keys.Any(key => string.Equals(key.Reference.Id, id, StringComparison.Ordinal)));

        if (named && !document.AddComponent<IOpenApiSecurityScheme>(id, scheme))
        {
            throw new InvalidOperationException($"The OpenAPI document already declares the security scheme '{id}'.");
        }
    }
}
