using System.Globalization;
using System.Reflection;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;

internal static class FieldValueParser
{
    private const NumberStyles Integer = NumberStyles.Integer;

    private const NumberStyles Decimal = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    private const DateTimeStyles Dates = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private static readonly MethodInfo FromGuidMethod = typeof(FieldValueParser).GetMethod(nameof(FromGuid), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static bool TryParse(string text, Type type, out object? value)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(type);

        var target = Nullable.GetUnderlyingType(type) ?? type;
        var culture = CultureInfo.InvariantCulture;
        value = null;
        if (target == typeof(string))
        {
            value = text;
            return true;
        }

        if (target.IsEnum)
        {
            return Enum.TryParse(target, text, ignoreCase: true, out value) && Enum.IsDefined(target, value!);
        }

        if (target.IsValueType && target.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>)))
        {
            if (!Guid.TryParse(text, out var id))
            {
                return false;
            }

            value = FromGuidMethod.MakeGenericMethod(target).Invoke(null, [id]);
            return true;
        }

        bool parsed;
        switch (target)
        {
            case var _ when target == typeof(bool):
                parsed = bool.TryParse(text, out var flag);
                value = flag;
                break;
            case var _ when target == typeof(int):
                parsed = int.TryParse(text, Integer, culture, out var int32);
                value = int32;
                break;
            case var _ when target == typeof(long):
                parsed = long.TryParse(text, Integer, culture, out var int64);
                value = int64;
                break;
            case var _ when target == typeof(decimal):
                parsed = decimal.TryParse(text, Decimal, culture, out var number);
                value = number;
                break;
            case var _ when target == typeof(double):
                parsed = double.TryParse(text, NumberStyles.Float, culture, out var real);
                value = real;
                break;
            case var _ when target == typeof(Guid):
                parsed = Guid.TryParse(text, out var guid);
                value = guid;
                break;
            case var _ when target == typeof(DateOnly):
                parsed = DateOnly.TryParseExact(text, "yyyy-MM-dd", culture, DateTimeStyles.None, out var date);
                value = date;
                break;
            case var _ when target == typeof(DateTimeOffset):
                parsed = DateTimeOffset.TryParse(text, culture, Dates, out var instant);
                value = instant;
                break;
            default:
                return false;
        }

        return parsed;
    }

    private static object FromGuid<TId>(Guid value)
        where TId : struct, IStronglyTypedId<TId> =>
        TId.From(value);
}
