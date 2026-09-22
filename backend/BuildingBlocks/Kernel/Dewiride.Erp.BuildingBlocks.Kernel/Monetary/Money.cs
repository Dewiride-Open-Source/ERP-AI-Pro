using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

[JsonConverter(typeof(MoneyJsonConverter))]
public readonly record struct Money : IComparable<Money>, ISpanFormattable, IParsable<Money>
{
    public Money(decimal amount, Currency currency)
    {
        if (!currency.IsDefined)
        {
            throw new ArgumentException("A money value needs a currency.", nameof(currency));
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; init; }

    public Currency Currency { get; init; }

    public bool IsZero => Amount == 0m;

    public bool IsNegative => Amount < 0m;

    public bool IsPositive => Amount > 0m;

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount, SameCurrency(left, right));

    public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount, SameCurrency(left, right));

    public static Money operator -(Money value) => new(-value.Amount, value.Currency);

    public static Money operator *(Money value, decimal factor) => new(value.Amount * factor, value.Currency);

    public static Money operator *(decimal factor, Money value) => value * factor;

    public static Money operator /(Money value, decimal divisor) => new(value.Amount / divisor, value.Currency);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    public static Money Negate(Money value) => -value;

    public static Money Multiply(Money value, decimal factor) => value * factor;

    public static Money Divide(Money value, decimal divisor) => value / divisor;

    public int CompareTo(Money other)
    {
        _ = SameCurrency(this, other);

        return Amount.CompareTo(other.Amount);
    }

    public Money RoundToMinorUnits() => new(decimal.Round(Amount, Currency.MinorUnits, MidpointRounding.AwayFromZero), Currency);

    // CGST Act 2017 s.170: tax, interest, penalty, fine or any other sum payable is rounded to the nearest rupee, fifty paise and above upwards.
    public Money RoundToWholeUnits() => new(decimal.Round(Amount, 0, MidpointRounding.AwayFromZero), Currency);

    public IReadOnlyList<Money> Allocate(params int[] ratios)
    {
        ArgumentNullException.ThrowIfNull(ratios);
        if (ratios.Length == 0 || ratios.Any(ratio => ratio < 0) || ratios.Sum() == 0)
        {
            throw new ArgumentException("Allocation needs at least one ratio, none negative and not all zero.", nameof(ratios));
        }

        var scale = Pow10(Currency.MinorUnits);
        var total = decimal.ToInt64(decimal.Round(Amount * scale, 0, MidpointRounding.AwayFromZero));
        var sum = ratios.Sum(ratio => (long)ratio);
        var shares = new long[ratios.Length];
        var remainders = new decimal[ratios.Length];
        long allocated = 0;
        for (var i = 0; i < ratios.Length; i++)
        {
            var exact = (decimal)total * ratios[i] / sum;
            shares[i] = (long)Math.Truncate(exact);
            remainders[i] = exact - shares[i];
            allocated += shares[i];
        }

        var remaining = total - allocated;
        var step = remaining >= 0 ? 1 : -1;
        foreach (var index in Enumerable.Range(0, ratios.Length).OrderByDescending(i => Math.Abs(remainders[i])).ThenBy(i => i))
        {
            if (remaining == 0)
            {
                break;
            }

            shares[index] += step;
            remaining -= step;
        }

        var currency = Currency;
        return shares.Select(share => new Money(share / scale, currency)).ToArray();
    }

    public IReadOnlyList<Money> Allocate(int parts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parts);

        return Allocate(Enumerable.Repeat(1, parts).ToArray());
    }

    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.Create(formatProvider ?? CultureInfo.InvariantCulture, $"{Amount.ToString(AmountFormat, formatProvider ?? CultureInfo.InvariantCulture)} {Currency.Code}");

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        var text = ToString(null, provider);
        if (text.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        text.AsSpan().CopyTo(destination);
        charsWritten = text.Length;
        return true;
    }

    public static Money Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var money) ? money : throw new FormatException($"'{s}' is not a money value of the form '<amount> <ISO 4217 code>'.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Money result)
    {
        result = default;
        if (s is null)
        {
            return false;
        }

        var parts = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !decimal.TryParse(parts[0], NumberStyles.Number, provider ?? CultureInfo.InvariantCulture, out var amount)
            || !Currency.TryFromCode(parts[1], out var currency))
        {
            return false;
        }

        result = new Money(amount, currency);
        return true;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, out Money result) => TryParse(s, CultureInfo.InvariantCulture, out result);

    private string AmountFormat => "F" + Currency.MinorUnits.ToString(CultureInfo.InvariantCulture);

    private static Currency SameCurrency(Money left, Money right) =>
        left.Currency == right.Currency
            ? left.Currency
            : throw new InvalidOperationException($"Money values in {left.Currency} and {right.Currency} cannot be combined.");

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }
}
