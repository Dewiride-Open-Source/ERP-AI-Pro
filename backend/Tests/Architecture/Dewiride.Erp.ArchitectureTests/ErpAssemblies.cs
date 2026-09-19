using System.Reflection;
using ArchUnitNET.Loader;
using Architecture = ArchUnitNET.Domain.Architecture;

namespace Dewiride.Erp.ArchitectureTests;

internal static class ErpAssemblies
{
    public const string Prefix = "Dewiride.Erp.";
    public const string ModulesPrefix = "Dewiride.Erp.Modules.";
    public const string BuildingBlocksPrefix = "Dewiride.Erp.BuildingBlocks.";
    public const string HostsPrefix = "Dewiride.Erp.Host.";
    public const string ContractsSuffix = ".Contracts";

    public static IReadOnlyList<Assembly> All { get; } = Load();

    public static IReadOnlyList<Assembly> Modules { get; } = All.Where(a => a.GetName().Name!.StartsWith(ModulesPrefix, StringComparison.Ordinal)).ToArray();

    public static IReadOnlyList<Assembly> ModuleImplementations { get; } = Modules.Where(a => !a.GetName().Name!.EndsWith(ContractsSuffix, StringComparison.Ordinal)).ToArray();

    public static IReadOnlyList<Assembly> BuildingBlocks { get; } = All.Where(a => a.GetName().Name!.StartsWith(BuildingBlocksPrefix, StringComparison.Ordinal)).ToArray();

    public static Architecture Architecture { get; } = new ArchLoader().LoadAssemblies([.. All]).Build();

    private static Assembly[] Load()
    {
        var files = Directory.EnumerateFiles(AppContext.BaseDirectory, $"{Prefix}*.dll")
            .Where(f => !Path.GetFileName(f).Contains("Tests", StringComparison.Ordinal) && !Path.GetFileName(f).StartsWith($"{Prefix}Testing", StringComparison.Ordinal));

        return files
            .Select(f => Assembly.Load(AssemblyName.GetAssemblyName(f)))
            .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
            .ToArray();
    }
}
