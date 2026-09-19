
namespace Dewiride.Erp.ArchitectureTests.Rules;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Solution_LoadsTheReferenceModule()
    {
        Assert.Contains(ErpAssemblies.ModuleImplementations, a => a.GetName().Name == "Dewiride.Erp.Modules.Platform.SystemInfo");
    }

    [Fact]
    public void ModuleImplementation_ReferencesOtherModulesOnlyThroughContracts()
    {
        var violations = new List<string>();

        foreach (var module in ErpAssemblies.ModuleImplementations)
        {
            var ownContracts = module.GetName().Name + ErpAssemblies.ContractsSuffix;
            foreach (var referenced in module.GetReferencedAssemblies().Select(r => r.Name!))
            {
                var isModule = referenced.StartsWith(ErpAssemblies.ModulesPrefix, StringComparison.Ordinal);
                var isHost = referenced.StartsWith(ErpAssemblies.HostsPrefix, StringComparison.Ordinal);
                var allowed = !isModule || referenced == ownContracts || referenced.EndsWith(ErpAssemblies.ContractsSuffix, StringComparison.Ordinal);
                if (isHost || !allowed)
                {
                    violations.Add($"{module.GetName().Name} -> {referenced}");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Contracts_ReferenceOnlyTheKernel()
    {
        var violations = ErpAssemblies.Modules
            .Where(a => a.GetName().Name!.EndsWith(ErpAssemblies.ContractsSuffix, StringComparison.Ordinal))
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => (Assembly: a.GetName().Name, Referenced: r.Name!)))
            .Where(x => x.Referenced.StartsWith(ErpAssemblies.Prefix, StringComparison.Ordinal) && x.Referenced != "Dewiride.Erp.BuildingBlocks.Kernel")
            .Select(x => $"{x.Assembly} -> {x.Referenced}")
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void BuildingBlocks_NeverReferenceModulesOrHosts()
    {
        var violations = ErpAssemblies.BuildingBlocks
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => (Assembly: a.GetName().Name, Referenced: r.Name!)))
            .Where(x => x.Referenced.StartsWith(ErpAssemblies.ModulesPrefix, StringComparison.Ordinal) || x.Referenced.StartsWith(ErpAssemblies.HostsPrefix, StringComparison.Ordinal))
            .Select(x => $"{x.Assembly} -> {x.Referenced}")
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void NoAssembly_ReferencesABannedPackage()
    {
        string[] banned = ["MediatR", "AutoMapper", "FluentAssertions", "Newtonsoft.Json", "Serilog", "NLog", "Swashbuckle.AspNetCore", "Moq", "Microsoft.SemanticKernel"];

        var violations = ErpAssemblies.All
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => (Assembly: a.GetName().Name, Referenced: r.Name!)))
            .Where(x => banned.Any(b => x.Referenced.Equals(b, StringComparison.OrdinalIgnoreCase) || x.Referenced.StartsWith(b + ".", StringComparison.OrdinalIgnoreCase)))
            .Select(x => $"{x.Assembly} -> {x.Referenced}")
            .ToList();

        Assert.Empty(violations);
    }
}
