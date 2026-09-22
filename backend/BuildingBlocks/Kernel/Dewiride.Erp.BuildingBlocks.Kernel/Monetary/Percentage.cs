using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

[JsonConverter(typeof(PercentageJsonConverter))]
public readonly record struct Percentage(decimal Value) : IComparable<Percentage>
{
    public static readonly Percentage Zero = new(0m);

    public static readonly Percentage Hundred = new(100m);

    public decimal AsFraction => Value / 100m;

    public static Percentage FromFraction(decimal fraction) => new(fraction * 100m);

    public Money Of(Money money) => money * AsFraction;

    public static Percentage operator +(Percentage left, Percentage right) => new(left.Value + right.Value);

    public static Percentage operator -(Percentage left, Percentage right) => new(left.Value - right.Value);

    public static bool operator <(Percentage left, Percentage right) => left.Value < right.Value;

    public static bool operator >(Percentage left, Percentage right) => left.Value > right.Value;

    public static bool operator <=(Percentage left, Percentage right) => left.Value <= right.Value;

    public static bool operator >=(Percentage left, Percentage right) => left.Value >= right.Value;

    public static Percentage Add(Percentage left, Percentage right) => left + right;

    public static Percentage Subtract(Percentage left, Percentage right) => left - right;

    public int CompareTo(Percentage other) => Value.CompareTo(other.Value);

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Value:0.####}%");
}
