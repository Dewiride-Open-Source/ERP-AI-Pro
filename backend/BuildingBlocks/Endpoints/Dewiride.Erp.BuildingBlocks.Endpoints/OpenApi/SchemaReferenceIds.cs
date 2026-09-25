using System.Collections.Concurrent;
using System.Text.Json.Serialization.Metadata;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.OpenApi;

internal sealed class SchemaReferenceIds(Func<JsonTypeInfo, string?> inner)
{
    private readonly ConcurrentDictionary<string, Type> _owners = new(StringComparer.Ordinal);

    public string? Create(JsonTypeInfo type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var id = inner(type);
        if (id is null)
        {
            return null;
        }

        // A value type and its nullable form share one schema, so they are the same owner.
        var clrType = Nullable.GetUnderlyingType(type.Type) ?? type.Type;
        var owner = _owners.GetOrAdd(id, clrType);

        return owner == clrType
            ? id
            : throw new InvalidOperationException(
                $"'{owner.FullName}' and '{clrType.FullName}' both map to the schema id '{id}'. The generated TypeScript client names its models after schema ids, so rename one of the types.");
    }
}
