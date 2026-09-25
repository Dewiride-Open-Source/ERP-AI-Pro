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

        var owner = _owners.GetOrAdd(id, type.Type);

        return owner == type.Type
            ? id
            : throw new InvalidOperationException(
                $"'{owner.FullName}' and '{type.Type.FullName}' both map to the schema id '{id}'. The generated TypeScript client names its models after schema ids, so rename one of the types.");
    }
}
