using System.Reflection;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Auditing;
using Dewiride.Erp.BuildingBlocks.Caching;
using Dewiride.Erp.BuildingBlocks.Configuration;
using Dewiride.Erp.BuildingBlocks.Idempotency;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Observability.Resilience;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.DesignTime;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Host.Composition;

public static class ErpHostComposition
{
    public static IHostApplicationBuilder AddErpPlatform(
        this IHostApplicationBuilder builder,
        Assembly hostAssembly,
        IReadOnlySet<string>? keyVaultReferences = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(hostAssembly);

        builder.UseOfflineConfigurationAtDesignTime();
        builder.AddErpConfiguration(hostAssembly, keyVaultReferences);
        builder.AddErpCaching();
        builder.AddErpHttpClientDefaults();
        builder.AddErpPersistence();
        builder.AddErpIdempotency();
        builder.AddErpAuditing();
        builder.AddErpAttachments();
        builder.AddModules(Modules.All);

        return builder;
    }
}
