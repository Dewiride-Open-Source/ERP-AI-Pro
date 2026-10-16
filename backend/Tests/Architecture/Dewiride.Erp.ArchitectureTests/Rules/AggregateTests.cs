using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.ArchitectureTests.Rules;

public sealed class AggregateTests
{
    [Fact]
    public void AggregateRoots_AreAuditableUnlessTheyAreAppendOnly()
    {
        var aggregates = ErpAssemblies.All
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && IsAggregateRoot(type))
            .ToList();
        var violations = aggregates
            .Where(type => !typeof(IAuditable).IsAssignableFrom(type) && !typeof(IAppendOnly).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToList();

        Assert.NotEmpty(aggregates);
        Assert.Empty(violations);
    }

    private static bool IsAggregateRoot(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
            {
                return true;
            }
        }

        return false;
    }
}
