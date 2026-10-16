using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

// Every entity type that implements IAuditable is recorded; there is no way to leave one out, because a trail that can be
// switched off for a type proves nothing about it. A recorded type is held to what the capture can see in the change tracker
// before the save: its key is a single Guid set in code, nothing it maps is owned or a collection of complex values, and no
// deletion of a principal cascades into it, because the store or a cascade the change tracker runs after the capture would
// change it without a record.
public static class RecordedEntityTypes
{
    public static bool IsRecorded(IReadOnlyEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return typeof(IAuditable).IsAssignableFrom(entityType.ClrType) && !entityType.IsOwned();
    }

    public static string? KeyOf(IReadOnlyEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return Inherited(entityType, ChangeRecordConventions.KeyAnnotation);
    }

    public static string? SubjectPropertyOf(IReadOnlyEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return Inherited(entityType, ChangeRecordConventions.SubjectAnnotation);
    }

    public static bool IsExcluded(IReadOnlyProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return property.FindAnnotation(ChangeRecordConventions.ExcludedAnnotation)?.Value is true;
    }

    public static IReadOnlyList<string> ProblemsOf(IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        var problems = new List<string>();
        if (!typeof(IAuditable).IsAssignableFrom(entityType.ClrType))
        {
            return problems;
        }

        var name = entityType.DisplayName();
        if (entityType.IsOwned())
        {
            problems.Add($"{name} implements IAuditable but is owned; its changes would go unrecorded, so map it as an entity type of its own or as a complex type");
            return problems;
        }

        if (!ChangeRecordConventions.IsWellFormedKey(KeyOf(entityType)))
        {
            problems.Add($"{name} declares no well-formed change key; call RecordChangesAs(\"<domain>.<module>.<feature>\") in its entity configuration");
        }

        if (entityType.GetTableName() is null)
        {
            problems.Add($"{name} maps to no table");
        }

        if (entityType.FindPrimaryKey() is not { Properties: [var key] } || ProviderType(key) != typeof(Guid) || key.ValueGenerated != ValueGenerated.Never)
        {
            problems.Add($"{name} needs a single key of a Guid provider type that is set in code, so the record names the row before the save");
        }

        problems.AddRange(entityType.GetNavigations()
            .Where(navigation => navigation.TargetEntityType.IsOwned())
            .Select(navigation => $"{name}.{navigation.Name} is an owned navigation, which the capture cannot record"));
        problems.AddRange(entityType.GetFlattenedComplexProperties()
            .Where(complex => complex.IsCollection)
            .Select(complex => $"{name}.{complex.Name} is a collection of complex values, which the capture cannot record"));
        problems.AddRange(entityType.GetForeignKeys()
            .Where(foreignKey => foreignKey.DeleteBehavior is not (DeleteBehavior.Restrict or DeleteBehavior.NoAction or DeleteBehavior.ClientNoAction))
            .Select(foreignKey => $"{name} is changed by a {foreignKey.DeleteBehavior} delete of {foreignKey.PrincipalEntityType.DisplayName()}; restrict the deletion instead"));

        if (SubjectPropertyOf(entityType) is { } subject && SubjectKeyOf(entityType) is not { IsRequired: true })
        {
            problems.Add($"{name} files its changes under {subject}, which must be a required single-property foreign key to a recorded type");
        }

        return problems;
    }

    public static IReadOnlyForeignKey? SubjectKeyOf(IReadOnlyEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (SubjectPropertyOf(entityType) is not { } subject || entityType.FindProperty(subject) is not { } property)
        {
            return null;
        }

        var foreignKeys = entityType.FindForeignKeys(property).ToList();

        return foreignKeys is [var foreignKey] && IsRecorded(foreignKey.PrincipalEntityType) && KeyOf(foreignKey.PrincipalEntityType) is not null
            ? foreignKey
            : null;
    }

    internal static Type ProviderType(IReadOnlyProperty property)
    {
        var type = property.GetValueConverter()?.ProviderClrType ?? property.ClrType;

        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static string? Inherited(IReadOnlyEntityType entityType, string annotation)
    {
        for (var type = entityType; type is not null; type = type.BaseType)
        {
            if (type.FindAnnotation(annotation)?.Value is string value)
            {
                return value;
            }
        }

        return null;
    }
}
