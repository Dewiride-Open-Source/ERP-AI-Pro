using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Security;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private static readonly PathString ApiReferencePath = new("/scalar");

    private static readonly string CallerHeaders = $"{HeaderNames.Authorization}, {HeaderNames.Cookie}";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            var headers = httpContext.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            var lifetime = CacheLifetime(httpContext);
            headers.CacheControl = lifetime is { } kept
                ? string.Create(CultureInfo.InvariantCulture, $"private, max-age={(long)kept.MaxAge.TotalSeconds}")
                : "no-store";
            if (lifetime is { ByCaller: true })
            {
                headers.Append(HeaderNames.Vary, CallerHeaders);
            }

            if (!httpContext.Request.Path.StartsWithSegments(ApiReferencePath))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            return Task.CompletedTask;
        }, context);

        return next(context);
    }

    private static (TimeSpan MaxAge, bool ByCaller)? CacheLifetime(HttpContext context)
    {
        if (context.Response.StatusCode != StatusCodes.Status200OK || !HttpMethods.IsGet(context.Request.Method))
        {
            return null;
        }

        var metadata = context.GetEndpoint()?.Metadata;
        if (metadata?.GetMetadata<ReferenceDataCacheMetadata>() is { } referenceData)
        {
            return (referenceData.MaxAge, false);
        }

        return metadata?.GetMetadata<CallerDataCacheMetadata>() is { } callerData ? (callerData.MaxAge, true) : null;
    }
}
