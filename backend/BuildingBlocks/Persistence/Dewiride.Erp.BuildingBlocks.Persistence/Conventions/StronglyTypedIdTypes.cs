using System.Collections.Concurrent;
using System.Reflection;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Conventions;

public static class StronglyTypedIdTypes
{
    private const string OwnAssemblyPrefix = "Dewiride.Erp.";

    private static readonly ConcurrentDictionary<Assembly, IReadOnlyList<Type>> Cache = new();

    public static IReadOnlyList<Type> In(Assembly contextAssembly)
    {
        ArgumentNullException.ThrowIfNull(contextAssembly);

        return Cache.GetOrAdd(contextAssembly, Discover);
    }

    public static bool IsStronglyTypedId(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.IsValueType
            && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>) && i.GetGenericArguments()[0] == type);
    }

    private static IReadOnlyList<Type> Discover(Assembly contextAssembly)
    {
        var referenced = contextAssembly.GetReferencedAssemblies()
            .Where(name => name.Name!.StartsWith(OwnAssemblyPrefix, StringComparison.Ordinal))
            .Select(Assembly.Load);

        return new[] { contextAssembly }.Concat(referenced)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsStronglyTypedId)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }
}
