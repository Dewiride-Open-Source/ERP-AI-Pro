using Dewiride.Erp.BuildingBlocks.Configuration;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Endpoints;
using Dewiride.Erp.BuildingBlocks.Endpoints.Security;
using Dewiride.Erp.BuildingBlocks.Idempotency;
using Dewiride.Erp.BuildingBlocks.Modules.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.Pipeline;

internal static class RequestPipeline
{
    public static WebApplicationBuilder AddRequestPipeline(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<KestrelServerOptions>().Configure<IOptions<ErpHostOptions>>((kestrel, host) =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = host.Value.MaxRequestBodyBytes;
        });

        // An empty trust list must mean no proxy is trusted: the middleware treats empty lists as "trust every peer".
        builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ErpHostOptions>>((forwarded, host) =>
        {
            forwarded.KnownIPNetworks.Clear();
            forwarded.KnownProxies.Clear();
            if (host.Value.KnownNetworks.Count == 0)
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwarded.ForwardLimit = 1;
            foreach (var network in host.Value.KnownNetworks)
            {
                forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        builder.Services.AddOptions<AllowedHostsOptions>().Configure<IOptions<ErpHostOptions>>((allowed, host) =>
            allowed.Hosts = host.Value.AllowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        builder.Services.AddOptions<RequestTimeoutOptions>().Configure<IOptions<ErpHostOptions>>((timeouts, host) =>
            timeouts.DefaultPolicy = new RequestTimeoutPolicy { Timeout = host.Value.RequestTimeout });

        return builder;
    }

    public static WebApplication UseRequestPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseErpConfigurationRefresh();
        app.UseErpEndpointPipeline();
        app.UseFeatureGate();
        app.UseErpIdempotency();

        return app;
    }
}
