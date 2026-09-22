using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Authorization;
using HostModules = Dewiride.Erp.Host.Composition.Modules;

namespace Dewiride.Erp.ArchitectureTests.Rules;

public sealed class ModuleCatalogTests : IClassFixture<ErpApiFactory>
{
    private static readonly string[] AnonymousRouteWhitelist =
    [
        "/healthz/live",
        "/healthz/ready",
        "/api/platform/system-info",
        "/api/platform/system-info/startups",
        "/api/platform/features",
        "/openapi/{documentName}.json",
    ];

    private static readonly string[] DevelopmentOnlyRoutePrefixes = ["/scalar"];

    private static readonly string[] GeneratedBaseTypes = ["Microsoft.EntityFrameworkCore.Migrations.Migration", "Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot"];

    private const string RequestsNamespaceSuffix = ".Endpoints.Requests";

    private readonly ErpApiFactory _factory;

    public ModuleCatalogTests(ErpApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void EveryModuleAssembly_DeclaresExactlyOnePublicModule()
    {
        foreach (var assembly in ErpAssemblies.ModuleImplementations)
        {
            var modules = assembly.GetExportedTypes().Where(t => typeof(IModule).IsAssignableFrom(t) && t is { IsAbstract: false }).ToList();
            Assert.True(modules.Count == 1, $"{assembly.GetName().Name} declares {modules.Count} public IModule implementations; expected exactly one.");
        }
    }

    [Fact]
    public void ModuleAssemblies_ExposeOnlyTheModuleClass()
    {
        var violations = ErpAssemblies.ModuleImplementations
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => !typeof(IModule).IsAssignableFrom(t) && !IsGeneratedPersistenceType(t) && !IsRequestRecord(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void HostCatalog_ListsEveryReferencedModuleExactlyOnce()
    {
        var registered = HostModules.All.Select(m => m.GetType().Assembly.GetName().Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var referenced = ErpAssemblies.ModuleImplementations.Select(a => a.GetName().Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(referenced, registered);
        Assert.Equal(registered.Count, registered.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ModuleDescriptors_FollowTheNamingConventions()
    {
        foreach (var module in HostModules.All)
        {
            var descriptor = module.Descriptor;
            var expectedAssembly = $"{ErpAssemblies.ModulesPrefix}{descriptor.Domain}.{descriptor.Name}";
            Assert.Equal(expectedAssembly, module.GetType().Assembly.GetName().Name);
            if (descriptor.Schema is not null)
            {
                Assert.Equal($"{Snake(descriptor.Domain)}_{Snake(descriptor.Name)}", descriptor.Schema);
            }

            Assert.Equal($"/{Kebab(descriptor.Domain)}/{Kebab(descriptor.Name)}", descriptor.RoutePrefix);
            Assert.Equal($"Erp.Modules.{descriptor.Domain}.{descriptor.Name}", descriptor.FeatureFlag);
            Assert.All(descriptor.Permissions, p => Assert.Matches("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*){3}$", p));
            Assert.All(descriptor.Permissions, p => Assert.StartsWith(descriptor.PermissionPrefix, p, StringComparison.Ordinal));
            Assert.All(descriptor.Capabilities, c => Assert.Matches("^[A-Z][A-Za-z0-9]*$", c.Name));
            Assert.Equal(descriptor.Capabilities.Count, descriptor.Capabilities.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    private static bool IsGeneratedPersistenceType(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (GeneratedBaseTypes.Contains(current.FullName, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRequestRecord(Type type) =>
        type.Namespace is { } ns && ns.EndsWith(RequestsNamespaceSuffix, StringComparison.Ordinal);

    private static string Kebab(string pascal) => System.Text.RegularExpressions.Regex.Replace(pascal, "(?<=[a-z0-9])(?=[A-Z])", "-").ToLowerInvariant();

    private static string Snake(string pascal) => System.Text.RegularExpressions.Regex.Replace(pascal, "(?<=[a-z0-9])(?=[A-Z])", "_").ToLowerInvariant();

    [Fact]
    public void AnonymousEndpoints_AreOnTheWhitelist()
    {
        using var scope = _factory.Services.CreateScope();
        var endpoints = scope.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var offenders = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null || e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(e => e.RoutePattern.RawText!.Length > 1 ? e.RoutePattern.RawText!.TrimEnd('/') : e.RoutePattern.RawText!)
            .Where(route => !AnonymousRouteWhitelist.Contains(route, StringComparer.Ordinal))
            .Where(route => !DevelopmentOnlyRoutePrefixes.Any(prefix => route.StartsWith(prefix, StringComparison.Ordinal)))
            .Distinct()
            .ToList();

        Assert.Empty(offenders);
    }
}
