using System.Reflection;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets.Seeding;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.Gst;

// Every typed set of the Contracts reads one seed file, whose columns its row record holds one by one, in order, each of its
// kind's type and nullable exactly when the column is optional, so a typed read can never meet a cell its record does not fit.
public sealed class ParameterSetTypesTests
{
    private static readonly IReadOnlyList<SeededSet> Files = StatutorySeedFiles.Read(StatutorySeedFiles.Embedded(), SeedProbes.AnyStateCode);

    private static readonly Type[] SetTypes = [.. typeof(IStatutoryParameters).Assembly.GetTypes()
        .Where(type => type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IStatutoryParameterSet<>)))
        .OrderBy(type => type.FullName, StringComparer.Ordinal)];

    public static TheoryData<string> SetTypeNames() => [.. SetTypes.Select(type => type.FullName!)];

    [Fact]
    public void SetTypes_AndSeedFiles_NameTheSameSetsOneToOne()
    {
        var keys = SetTypes.Select(KeyOf).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Files.Select(file => file.Set.Key).Order(StringComparer.Ordinal), keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SetTypeNames))]
    public void RowRecord_OfEachSet_HoldsItsFilesColumnsInOrderEachOfItsKindsType(string setTypeName)
    {
        var setType = SetTypes.Single(type => type.FullName == setTypeName);
        var columns = Files.Single(file => file.Set.Key == KeyOf(setType)).Set.Columns.Columns;
        var parameters = RowTypeOf(setType).GetConstructors().Single().GetParameters();
        var nullability = new NullabilityInfoContext();

        Assert.Equal(columns.Select(column => column.Name), parameters.Select(parameter => JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!)));
        foreach (var (column, parameter) in columns.Zip(parameters))
        {
            var valueType = Nullable.GetUnderlyingType(parameter.ParameterType);
            var nullable = valueType is not null || nullability.Create(parameter).WriteState == NullabilityState.Nullable;
            Assert.True(column.IsOptional == nullable, $"{setType.Name}: {parameter.Name} is {(nullable ? string.Empty : "not ")}nullable, but column {column.Name} is {(column.IsOptional ? string.Empty : "not ")}optional.");
            AssertKind(column, valueType ?? parameter.ParameterType);
        }
    }

    [Theory]
    [MemberData(nameof(SetTypeNames))]
    public void SetRecord_OfEachSet_IsBuiltFromAVersionAndAListOfItsRows(string setTypeName)
    {
        var parameters = SetTypes.Single(type => type.FullName == setTypeName).GetConstructors().Single().GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(StatutoryParameterVersion), parameters[0].ParameterType);
        Assert.Equal(typeof(IReadOnlyList<>), parameters[1].ParameterType.GetGenericTypeDefinition());
    }

    private static void AssertKind(ParameterColumn column, Type type)
    {
        switch (column.Kind)
        {
            case CellKind.Text or CellKind.Code or CellKind.StateCode:
                Assert.Equal(typeof(string), type);
                break;
            case CellKind.Integer:
                Assert.Equal(typeof(int), type);
                break;
            case CellKind.Percent:
                Assert.Equal(typeof(Percentage), type);
                break;
            case CellKind.Amount:
                Assert.Equal(typeof(Money), type);
                break;
            case CellKind.Choice:
                Assert.True(type.IsEnum, $"Column {column.Name} is a choice, so its parameter is an enum, not {type.Name}.");
                Assert.Equal(column.Choices.Select(choice => choice.Value), Enum.GetNames(type).Select(JsonNamingPolicy.CamelCase.ConvertName));
                break;
            default:
                Assert.Fail($"Column {column.Name} is of a kind the typed sets do not know: {column.Kind}.");
                break;
        }
    }

    private static string KeyOf(Type setType) =>
        (string)setType.GetProperty(nameof(IStatutoryParameterSet<>.Key), BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    private static Type RowTypeOf(Type setType) => setType.GetConstructors().Single().GetParameters()[1].ParameterType.GetGenericArguments()[0];
}
