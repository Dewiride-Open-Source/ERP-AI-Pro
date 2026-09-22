using System.Linq.Expressions;
using System.Reflection;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;

internal sealed class FilterPredicateBuilder<T>
{
    private static readonly MethodInfo StringContains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private static readonly MethodInfo EnumerableContains = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == nameof(Enumerable.Contains) && method.GetParameters().Length == 2);

    private readonly ParameterExpression _parameter = Expression.Parameter(typeof(T), "entity");

    private Expression? _body;

    public void Add(FilterableField field, FilterOperator @operator, IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(values);

        var member = ParameterReplacer.Replace(field.Selector.Body, field.Selector.Parameters[0], _parameter);
        Expression comparison = @operator switch
        {
            FilterOperator.Equal => Expression.Equal(member, Constant(values[0], field.ValueType)),
            FilterOperator.NotEqual => Expression.NotEqual(member, Constant(values[0], field.ValueType)),
            FilterOperator.GreaterThan => Expression.GreaterThan(member, Constant(values[0], field.ValueType)),
            FilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(member, Constant(values[0], field.ValueType)),
            FilterOperator.LessThan => Expression.LessThan(member, Constant(values[0], field.ValueType)),
            FilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(member, Constant(values[0], field.ValueType)),
            FilterOperator.Contains => Expression.Call(member, StringContains, Constant(values[0], field.ValueType)),
            FilterOperator.In => Expression.Call(EnumerableContains.MakeGenericMethod(field.ValueType), TypedArray(values, field.ValueType), member),
            _ => throw new ArgumentOutOfRangeException(nameof(@operator), @operator, "Unknown filter operator."),
        };
        _body = _body is null ? comparison : Expression.AndAlso(_body, comparison);
    }

    public ResolvedFilter<T> Build() =>
        _body is null ? ResolvedFilter<T>.None : new ResolvedFilter<T>(Expression.Lambda<Func<T, bool>>(_body, _parameter));

    private static MemberExpression Constant(object? value, Type type)
    {
        var boxType = typeof(ValueBox<>).MakeGenericType(typeof(T), type);

        return Expression.Property(Expression.Constant(Activator.CreateInstance(boxType, value), boxType), nameof(ValueBox<>.Value));
    }

    private static MemberExpression TypedArray(IReadOnlyList<object?> values, Type type)
    {
        var array = Array.CreateInstance(type, values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            array.SetValue(values[i], i);
        }

        return Constant(array, type.MakeArrayType());
    }

    private sealed class ValueBox<TValue>(TValue value)
    {
        public TValue Value { get; } = value;
    }
}
