using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

[JsonConverter(typeof(CurrencyJsonConverter))]
public readonly record struct Currency
{
    public const int CodeLength = 3;

    public static readonly Currency Inr = Iso4217Currencies.Find("INR");

    public static readonly Currency Usd = Iso4217Currencies.Find("USD");

    public static readonly Currency Eur = Iso4217Currencies.Find("EUR");

    public static readonly Currency Gbp = Iso4217Currencies.Find("GBP");

    public static readonly Currency Aed = Iso4217Currencies.Find("AED");

    public static readonly Currency Sgd = Iso4217Currencies.Find("SGD");

    public static readonly Currency Aud = Iso4217Currencies.Find("AUD");

    public static readonly Currency Cad = Iso4217Currencies.Find("CAD");

    public static readonly Currency Jpy = Iso4217Currencies.Find("JPY");

    internal Currency(string code, int minorUnits, string name)
    {
        Code = code;
        MinorUnits = minorUnits;
        Name = name;
    }

    public string Code { get; }

    public int MinorUnits { get; }

    public string Name { get; }

    public static IReadOnlyList<Currency> All => Iso4217Currencies.All;

    public static Currency FromCode(string code) =>
        TryFromCode(code, out var currency)
            ? currency
            : throw new ArgumentException($"'{code}' is not the code of an ISO 4217 currency such as 'INR'.", nameof(code));

    public static bool TryFromCode(string? code, out Currency currency) => Iso4217Currencies.TryFind(code, out currency);

    public bool Equals(Currency other) => string.Equals(Code, other.Code, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code ?? string.Empty);

    public override string ToString() => Code ?? string.Empty;

    [MemberNotNullWhen(true, nameof(Code), nameof(Name))]
    public bool IsDefined => Code is not null;
}
