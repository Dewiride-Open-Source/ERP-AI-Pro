using Dewiride.Erp.BuildingBlocks.Configuration;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Endpoints;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.Pipeline;

internal static class RequestPipeline
{
    public static WebApplicationBuilder AddRequestPipeline(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

        builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ErpHostOptions>>((forwarded, host) =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwarded.ForwardLimit = 1;
            forwarded.KnownIPNetworks.Clear();
            forwarded.KnownProxies.Clear();
            foreach (var network in host.Value.KnownNetworks)
            {
                forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        builder.Services.AddHostFiltering(options =>
        {
            var allowed = builder.Configuration[$"{ErpHostOptions.SectionName}:AllowedHosts"] ?? new ErpHostOptions().AllowedHosts;
            options.AllowedHosts = allowed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            options.AllowEmptyHosts = false;
        });

        return builder;
    }

    public static WebApplication UseRequestPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseErpConfigurationRefresh();
        app.UseHostFiltering();
        app.UseErpEndpointPipeline();

        return app;
    }
}
