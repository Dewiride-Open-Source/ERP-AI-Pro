using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence;

public sealed class MigrationStatusTests
{
    private const string Schema = "finance_sales";

    private static readonly string[] Defined = ["20260101000000_Initial", "20260201000000_AddLines", "20260301000000_AddNotes"];

    [Fact]
    public void Of_NothingApplied_ReportsEveryDefinedMigrationPendingInDefinedOrder()
    {
        var status = MigrationStatus.Of(Schema, Defined, []);

        Assert.Equal(Schema, status.Schema);
        Assert.Equal(Defined, status.Pending);
        Assert.Empty(status.Unknown);
    }

    [Fact]
    public void Of_EarlierMigrationsApplied_ReportsTheLaterOnesPending()
    {
        var status = MigrationStatus.Of(Schema, Defined, Defined[..2]);

        Assert.Equal([Defined[2]], status.Pending);
        Assert.Empty(status.Unknown);
    }

    [Fact]
    public void Of_EveryDefinedMigrationApplied_ReportsNothingPendingOrUnknown()
    {
        var status = MigrationStatus.Of(Schema, Defined, Defined);

        Assert.Empty(status.Pending);
        Assert.Empty(status.Unknown);
    }

    [Fact]
    public void Of_HistoryHoldsMigrationsTheContextDoesNotDefine_ReportsThemUnknownInHistoryOrder()
    {
        string[] later = ["20260401000000_FromALaterRelease", "20260501000000_FromTheReleaseAfter"];

        var status = MigrationStatus.Of(Schema, Defined, [.. Defined, .. later]);

        Assert.Empty(status.Pending);
        Assert.Equal(later, status.Unknown);
    }

    [Fact]
    public void Of_HistoryLacksADefinedMigrationAndHoldsAnUnknownOne_ReportsBoth()
    {
        var status = MigrationStatus.Of(Schema, Defined, [Defined[0], "20260115000000_FromAnotherRelease", Defined[2]]);

        Assert.Equal([Defined[1]], status.Pending);
        Assert.Equal(["20260115000000_FromAnotherRelease"], status.Unknown);
    }

    [Fact]
    public void Of_AppliedIdDiffersOnlyInCase_MatchesTheDefinedMigration()
    {
        var status = MigrationStatus.Of(Schema, Defined, [.. Defined.Select(id => id.ToUpperInvariant())]);

        Assert.Empty(status.Pending);
        Assert.Empty(status.Unknown);
    }
}
