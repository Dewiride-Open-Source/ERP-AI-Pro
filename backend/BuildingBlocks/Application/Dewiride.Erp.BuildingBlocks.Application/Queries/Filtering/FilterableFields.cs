using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;

public sealed class FilterableFields<T>
{
    private readonly Dictionary<string, FilterableField> _fields = new(FieldName.Comparer);

    public IReadOnlyCollection<string> Names => _fields.Keys;

    public FilterableFields<T> Add<TValue>(string name, Expression<Func<T, TValue>> selector, params IReadOnlyList<FilterOperator> operators)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(operators);
        if (!FieldName.IsValid(name))
        {
            throw new ArgumentException($"'{name}' is not a field name; use letters and digits starting with a letter.", nameof(name));
        }

        _fields.Add(name, new FilterableField(name, selector, typeof(TValue), operators.Count > 0 ? operators : DefaultOperators(typeof(TValue))));

        return this;
    }

    internal bool TryGet(string name, [NotNullWhen(true)] out FilterableField? field) => _fields.TryGetValue(name, out field);

    private static IReadOnlyList<FilterOperator> DefaultOperators(Type valueType)
    {
        var type = Nullable.GetUnderlyingType(valueType) ?? valueType;
        if (type == typeof(string))
        {
            return FilterOperators.Text;
        }

        if (type == typeof(bool))
        {
            return FilterOperators.Flag;
        }

        if (type.IsEnum || type == typeof(Guid) || IsStronglyTypedId(type))
        {
            return FilterOperators.Equality;
        }

        return FilterOperators.Comparable;
    }

    private static bool IsStronglyTypedId(Type type) =>
        type.IsValueType && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>));
}
