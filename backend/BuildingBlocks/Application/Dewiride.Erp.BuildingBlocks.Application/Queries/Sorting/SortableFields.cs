using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;

public sealed class SortableFields<T>
{
    private readonly Dictionary<string, LambdaExpression> _fields = new(FieldName.Comparer);

    public IReadOnlyCollection<string> Names => _fields.Keys;

    public SortableFields<T> Add<TKey>(string name, Expression<Func<T, TKey>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (!FieldName.IsValid(name))
        {
            throw new ArgumentException($"'{name}' is not a field name; use letters and digits starting with a letter.", nameof(name));
        }

        _fields.Add(name, selector);

        return this;
    }

    internal bool TryGet(string name, [NotNullWhen(true)] out LambdaExpression? selector) => _fields.TryGetValue(name, out selector);
}
