using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

// A change key names a recorded type in its records and to their readers, so it never follows a table or class name: renaming
// one needs a data migration of the records that carry it. It is three lower-case kebab segments, <domain>.<module>.<feature>,
// the first two of a module's type being the module's permission prefix.
public static partial class ChangeRecordConventions
{
    internal const string KeyAnnotation = "Erp:ChangeRecords:Key";

    internal const string ExcludedAnnotation = "Erp:ChangeRecords:Excluded";

    internal const string SubjectAnnotation = "Erp:ChangeRecords:Subject";

    public static EntityTypeBuilder<TEntity> RecordChangesAs<TEntity>(this EntityTypeBuilder<TEntity> builder, string key)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!IsWellFormedKey(key))
        {
            throw new ArgumentException($"The change key '{key}' of {typeof(TEntity).Name} must be three lower-case kebab segments, <domain>.<module>.<feature>, of at most {ChangeRecordMapping.KeyMaxLength} characters.", nameof(key));
        }

        builder.HasAnnotation(KeyAnnotation, key);

        return builder;
    }

    public static PropertyBuilder<TProperty> ExcludeFromChangeRecords<TProperty>(this PropertyBuilder<TProperty> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasAnnotation(ExcludedAnnotation, true);

        return builder;
    }

    public static EntityTypeBuilder<TEntity> FileChangesUnder<TEntity, TProperty>(this EntityTypeBuilder<TEntity> builder, Expression<Func<TEntity, TProperty>> foreignKey)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(foreignKey);

        builder.HasAnnotation(SubjectAnnotation, builder.Property(foreignKey).Metadata.Name);

        return builder;
    }

    public static bool IsWellFormedKey(string? key) => key is not null && key.Length <= ChangeRecordMapping.KeyMaxLength && KeyPattern().IsMatch(key);

    // \z rather than $, which would also match before a trailing line feed.
    [GeneratedRegex(@"^[a-z][a-z0-9]*(-[a-z0-9]+)*(\.[a-z][a-z0-9]*(-[a-z0-9]+)*){2}\z")]
    private static partial Regex KeyPattern();
}
