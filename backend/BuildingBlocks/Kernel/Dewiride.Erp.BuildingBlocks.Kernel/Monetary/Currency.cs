using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

[JsonConverter(typeof(CurrencyJsonConverter))]
public readonly record struct Currency
{
    public const int CodeLength = 3;

    public static readonly Currency Inr = new("INR", 2);

    public static readonly Currency Usd = new("USD", 2);

    public static readonly Currency Eur = new("EUR", 2);

    public static readonly Currency Gbp = new("GBP", 2);

    public static readonly Currency Aed = new("AED", 2);

    public static readonly Currency Sgd = new("SGD", 2);

    public static readonly Currency Aud = new("AUD", 2);

    public static readonly Currency Cad = new("CAD", 2);

    public static readonly Currency Jpy = new("JPY", 0);

    private static readonly ReadOnlyDictionary<string, Currency> ByCode = new(new Dictionary<string, Currency>(StringComparer.Ordinal)
    {
        [Inr.Code] = Inr,
        [Usd.Code] = Usd,
        [Eur.Code] = Eur,
        [Gbp.Code] = Gbp,
        [Aed.Code] = Aed,
        [Sgd.Code] = Sgd,
        [Aud.Code] = Aud,
        [Cad.Code] = Cad,
        [Jpy.Code] = Jpy,
    });

    private Currency(string code, int minorUnits)
    {
        Code = code;
        MinorUnits = minorUnits;
    }

    public string Code { get; }

    public int MinorUnits { get; }

    public static IReadOnlyCollection<Currency> All => ByCode.Values;

    public static Currency FromCode(string code) =>
        TryFromCode(code, out var currency)
            ? currency
            : throw new ArgumentException($"'{code}' is not a supported ISO 4217 currency code; supported codes are {string.Join(", ", ByCode.Keys)}.", nameof(code));

    public static bool TryFromCode(string? code, out Currency currency)
    {
        if (code is not null && ByCode.TryGetValue(code, out currency))
        {
            return true;
        }

        currency = default;
        return false;
    }

    public bool Equals(Currency other) => string.Equals(Code, other.Code, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code ?? string.Empty);

    public override string ToString() => Code ?? string.Empty;

    [MemberNotNullWhen(true, nameof(Code))]
    public bool IsDefined => Code is not null;
}
