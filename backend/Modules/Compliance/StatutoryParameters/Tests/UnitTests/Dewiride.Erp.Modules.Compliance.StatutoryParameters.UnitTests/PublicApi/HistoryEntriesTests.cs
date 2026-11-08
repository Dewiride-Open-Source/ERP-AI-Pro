using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.PublicApi;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.PublicApi;

public sealed class HistoryEntriesTests
{
    public static TheoryData<string> AsOfDates() => [.. Enum.GetNames<AsOfDate>()];

    [Fact]
    public void AsOfDates_AreTheContractsMembersInTheirOrder()
    {
        Assert.Equal(Enum.GetNames<AsOfDate>(), Enum.GetNames<ParameterAsOf>());
    }

    [Theory]
    [MemberData(nameof(AsOfDates))]
    public void AsOfOf_EachAsOfDate_IsTheContractsMemberOfItsName(string name)
    {
        Assert.Equal(name, HistoryEntries.AsOfOf(Enum.Parse<AsOfDate>(name)).ToString());
    }

    [Fact]
    public void CellKinds_AreTheContractsColumnKindsInTheirOrder()
    {
        Assert.Equal(Enum.GetNames<CellKind>(), Enum.GetNames<ParameterColumnKind>());
    }
}
