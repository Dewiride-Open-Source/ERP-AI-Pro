using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Security;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private static readonly PathString ApiReferencePath = new("/scalar");

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
            headers.CacheControl = "no-store";
            if (!httpContext.Request.Path.StartsWithSegments(ApiReferencePath))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            return Task.CompletedTask;
        }, context);

        return next(context);
    }
}
