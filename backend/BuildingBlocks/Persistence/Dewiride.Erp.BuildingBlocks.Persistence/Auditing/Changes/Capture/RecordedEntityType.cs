using System.Numerics;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes.Capture;

// What the capture reads from an entry of one recorded type, worked out once per type of a model and kept on the model. The key,
// the audit, deletion and version stamps, store-generated values and the properties a configuration excludes are no part of a
// record's values: the key has a column of its own, the stamps are the record's time and actor, the operation says whether
// the row was deleted or restored, and a store-generated value does not exist yet when the capture runs.
internal sealed class RecordedEntityType
{
    private const string PlanAnnotation = "Erp:ChangeRecords:Plan";

    private static readonly string[] AuditStamps = [nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedBy), nameof(IAuditable.ModifiedAt), nameof(IAuditable.ModifiedBy)];

    private static readonly string[] DeletionStamps = [nameof(ISoftDeletable.IsDeleted), nameof(ISoftDeletable.DeletedAt), nameof(ISoftDeletable.DeletedBy)];

    private RecordedEntityType(string key, IProperty keyProperty, string subjectKey, IProperty? subjectProperty, bool isSoftDeletable, IReadOnlyList<RecordedProperty> properties)
    {
        Key = key;
        KeyProperty = keyProperty;
        SubjectKey = subjectKey;
        SubjectProperty = subjectProperty;
        IsSoftDeletable = isSoftDeletable;
        Properties = properties;
    }

    public string Key { get; }

    public IProperty KeyProperty { get; }

    public string SubjectKey { get; }

    public IProperty? SubjectProperty { get; }

    public bool IsSoftDeletable { get; }

    public IReadOnlyList<RecordedProperty> Properties { get; }

    public static RecordedEntityType Of(IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return entityType.GetOrAddRuntimeAnnotationValue(PlanAnnotation, static type => Build(type!), entityType);
    }

    private static RecordedEntityType Build(IEntityType entityType)
    {
        var problems = RecordedEntityTypes.ProblemsOf(entityType);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException($"The changes of {entityType.DisplayName()} cannot be recorded: {string.Join("; ", problems)}.");
        }

        var key = RecordedEntityTypes.KeyOf(entityType)!;
        var subject = (IForeignKey?)RecordedEntityTypes.SubjectKeyOf(entityType);
        var properties = entityType.GetFlattenedProperties()
            .Where(property => IsRecorded(entityType, property))
            .Select(Describe)
            .ToList();

        return new RecordedEntityType(
            key,
            entityType.FindPrimaryKey()!.Properties[0],
            subject is null ? key : RecordedEntityTypes.KeyOf(subject.PrincipalEntityType)!,
            subject?.Properties[0],
            typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType),
            properties);
    }

    private static bool IsRecorded(IEntityType entityType, IProperty property) =>
        !property.IsPrimaryKey()
        && property.ValueGenerated == ValueGenerated.Never
        && !RecordedEntityTypes.IsExcluded(property)
        && !IsStamp(entityType, property)
        && property != entityType.FindDiscriminatorProperty();

    private static bool IsStamp(IEntityType entityType, IProperty property) =>
        property.DeclaringType is IEntityType
        && ((typeof(IAuditable).IsAssignableFrom(entityType.ClrType) && AuditStamps.Contains(property.Name))
            || (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType) && DeletionStamps.Contains(property.Name))
            || (typeof(IVersioned).IsAssignableFrom(entityType.ClrType) && property.Name == nameof(IVersioned.RowVersion)));

    private static RecordedProperty Describe(IProperty property)
    {
        if (property.IsPrimitiveCollection)
        {
            return new RecordedProperty(property, PathOf(property), ChangeValueKind.List, null);
        }

        var reference = property.GetContainingForeignKeys()
            .FirstOrDefault(foreignKey => foreignKey.Properties.Count == 1 && RecordedEntityTypes.IsRecorded(foreignKey.PrincipalEntityType));
        if (reference is not null && RecordedEntityTypes.KeyOf(reference.PrincipalEntityType) is { } referenced)
        {
            return new RecordedProperty(property, PathOf(property), ChangeValueKind.Reference, referenced);
        }

        var kind = KindOf(Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType)
            ?? KindOf(RecordedEntityTypes.ProviderType(property))
            ?? ChangeValueKind.Text;

        return new RecordedProperty(property, PathOf(property), kind, null);
    }

    private static ChangeValueKind? KindOf(Type type)
    {
        if (type.IsEnum)
        {
            return ChangeValueKind.Enum;
        }

        if (type == typeof(bool))
        {
            return ChangeValueKind.Boolean;
        }

        if (type == typeof(string) || type == typeof(char) || type == typeof(TimeOnly) || type == typeof(TimeSpan))
        {
            return ChangeValueKind.Text;
        }

        if (type == typeof(DateOnly))
        {
            return ChangeValueKind.Date;
        }

        if (type == typeof(DateTimeOffset) || type == typeof(DateTime))
        {
            return ChangeValueKind.Instant;
        }

        if (type == typeof(byte[]))
        {
            return ChangeValueKind.Binary;
        }

        if (type == typeof(Guid) || StronglyTypedIdTypes.IsStronglyTypedId(type))
        {
            return ChangeValueKind.Id;
        }

        return type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumberBase<>)) ? ChangeValueKind.Number : null;
    }

    private static string PathOf(IReadOnlyProperty property)
    {
        var path = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
        for (var type = property.DeclaringType; type is IReadOnlyComplexType complexType; type = complexType.ComplexProperty.DeclaringType)
        {
            path = $"{JsonNamingPolicy.CamelCase.ConvertName(complexType.ComplexProperty.Name)}.{path}";
        }

        return path;
    }
}
