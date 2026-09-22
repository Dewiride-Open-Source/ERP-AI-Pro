using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Conventions;

public sealed class StronglyTypedIdConventionTests
{
    [Fact]
    public void Converter_RoundTrip_PreservesTheGuid()
    {
        var converter = new StronglyTypedIdConverter<SampleId>();
        var id = SampleId.Create();

        var stored = converter.ConvertToProviderTyped(id);
        var restored = converter.ConvertFromProviderTyped(stored);

        Assert.Equal(id.Value, stored);
        Assert.Equal(id, restored);
    }

    [Fact]
    public void Create_TwoIds_AreVersion7AndDistinct()
    {
        var first = SampleId.Create();
        var second = SampleId.Create();

        Assert.Equal(7, first.Value.Version);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void In_TestAssembly_DiscoversTheIdStructAndNothingElse()
    {
        var types = StronglyTypedIdTypes.In(typeof(StronglyTypedIdConventionTests).Assembly);

        Assert.Contains(typeof(SampleId), types);
        Assert.DoesNotContain(typeof(NotAnId), types);
        Assert.All(types, type => Assert.True(StronglyTypedIdTypes.IsStronglyTypedId(type)));
    }

    [Theory]
    [InlineData(typeof(SampleId), true)]
    [InlineData(typeof(NotAnId), false)]
    [InlineData(typeof(Guid), false)]
    [InlineData(typeof(string), false)]
    public void IsStronglyTypedId_Type_MatchesOnlyValueTypesImplementingTheInterfaceForThemselves(Type type, bool expected)
    {
        Assert.Equal(expected, StronglyTypedIdTypes.IsStronglyTypedId(type));
    }

    [Fact]
    public void UtcConverter_OffsetValue_StoresAndReadsUtc()
    {
        var converter = new UtcDateTimeOffsetConverter();
        var india = new DateTimeOffset(2026, 4, 1, 9, 30, 0, TimeSpan.FromHours(5.5));

        var stored = converter.ConvertToProviderTyped(india);
        var restored = converter.ConvertFromProviderTyped(new DateTimeOffset(2026, 4, 1, 4, 0, 0, TimeSpan.FromHours(1)));

        Assert.Equal(TimeSpan.Zero, stored.Offset);
        Assert.Equal(india, stored);
        Assert.Equal(TimeSpan.Zero, restored.Offset);
    }

    internal readonly record struct SampleId(Guid Value) : IStronglyTypedId<SampleId>
    {
        public static SampleId Create() => new(Guid.CreateVersion7());

        public static SampleId From(Guid value) => new(value);
    }

    internal readonly record struct NotAnId(Guid Value);
}
