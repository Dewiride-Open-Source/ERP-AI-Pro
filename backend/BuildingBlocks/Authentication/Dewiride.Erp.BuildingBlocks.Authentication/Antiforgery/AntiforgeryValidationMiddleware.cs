using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;

// Only the session cookie reaches the API without the page asking for it, so only a request it signs in can be forged; an
// anonymous request, or one signed in by a header, passes unchecked. The token is read from the header, which another
// site's form cannot set and its scripts cannot send here without CORS, and from the body only on an endpoint that reads a
// form (IAntiforgeryMetadata requiring validation, as on the sign-out form a page posts), so an upload is refused before a
// byte of it is read. This runs ahead of the framework middleware, whose refusal would make any later read of the form throw.
internal sealed partial class AntiforgeryValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery, ILogger<AntiforgeryValidationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IAntiforgeryMetadata>();
        if (!ChangesData(context.Request.Method)
            || metadata is { RequiresValidation: false }
            || !(await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false)).Succeeded)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var refusal = await RefusalAsync(context, readsAForm: metadata is { RequiresValidation: true }).ConfigureAwait(false);
        if (refusal is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        LogRefused(logger, context.Request.Method, context.Request.Path, refusal);
        await AntiforgeryProblems.WriteAsync(context, problemDetails, refusal).ConfigureAwait(false);
    }

    private static bool ChangesData(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private async Task<string?> RefusalAsync(HttpContext context, bool readsAForm)
    {
        var request = context.Request;
        var fromForm = StringValues.IsNullOrEmpty(request.Headers[AntiforgeryTokens.HeaderName]);
        if (string.IsNullOrEmpty(request.Cookies[AntiforgeryTokens.CookieName]) || (fromForm && !(readsAForm && request.HasFormContentType)))
        {
            return AntiforgeryProblems.TokenMissing;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return fromForm && !CarriesFormToken(context) ? AntiforgeryProblems.TokenMissing : AntiforgeryProblems.TokenInvalid;
        }
    }

    // Validation read the form when it looked for the token there, so the form it kept is inspected, never the body again; a
    // form it could not read counts as carrying no token.
    private static bool CarriesFormToken(HttpContext context) =>
        context.Features.Get<IFormFeature>()?.Form is { } form && !StringValues.IsNullOrEmpty(form[AntiforgeryTokens.FormFieldName]);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused {Method} {Path} signed in with the session cookie: {Code}")]
    private static partial void LogRefused(ILogger logger, string method, PathString path, string code);
}
