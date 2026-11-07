using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Returns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Gst;

// Golden values of the standing GST return due dates, the two State lists of the quarterly GSTR-3B and the turnover limits for
// tax periods from January 2021 (owner decision of 2026-11-07: each pinned value beside its instrument). The GSTR-9 exemption is
// left out of every year (owner decision of 2026-11-07), so no limit of that name exists on any day.
[Collection(StatutoryRows.Name)]
public sealed class ReturnGoldenTests(ErpApiFactory factory)
{
    private static readonly DateOnly FirstPeriod = new(2021, 1, 1);

    [Theory]
    [InlineData("gstr-1-monthly", "GSTR-1", ReturnFilers.Monthly, ReturnPeriod.Month, null, 11, 1, "Notification No. 83/2020-Central Tax, paragraph 1")]
    [InlineData("gstr-1-quarterly", "GSTR-1", ReturnFilers.Quarterly, ReturnPeriod.Quarter, null, 13, 1, "Notification No. 83/2020-Central Tax, paragraph 1, first proviso")]
    [InlineData("iff", "IFF", ReturnFilers.Quarterly, ReturnPeriod.FirstTwoMonthsOfQuarter, 1, 13, 1, "rule 59(2), as substituted by Notification No. 82/2020-Central Tax")]
    [InlineData("gstr-3b-monthly", "GSTR-3B", ReturnFilers.Monthly, ReturnPeriod.Month, null, 20, 1, "rule 61(1)(i), as substituted by Notification No. 82/2020-Central Tax")]
    [InlineData("gstr-3b-quarterly-1", "GSTR-3B", ReturnFilers.Quarterly, ReturnPeriod.Quarter, null, 22, 1, "rule 61(1)(ii), Table, serial number 1")]
    [InlineData("gstr-3b-quarterly-2", "GSTR-3B", ReturnFilers.Quarterly, ReturnPeriod.Quarter, null, 24, 1, "rule 61(1)(ii), Table, serial number 2")]
    [InlineData("pmt-06", "PMT-06", ReturnFilers.Quarterly, ReturnPeriod.FirstTwoMonthsOfQuarter, null, 25, 1, "rule 61(3), as substituted by Notification No. 82/2020-Central Tax")]
    [InlineData("gstr-9", "GSTR-9", ReturnFilers.All, ReturnPeriod.FinancialYear, null, 31, 9, "rule 80(1), as substituted by Notification No. 30/2021-Central Tax")]
    [InlineData("gstr-9c", "GSTR-9C", ReturnFilers.All, ReturnPeriod.FinancialYear, null, 31, 9, "rule 80(3), as substituted by Notification No. 30/2021-Central Tax")]
    public async Task DueDate_OfEachReturn_IsTheDayAndMonthItsInstrumentSets(string rule, string form, ReturnFilers filers, ReturnPeriod period, int? opensDay, int dueDay, int monthsAfter, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        foreach (var day in new[] { FirstPeriod, new DateOnly(2026, 11, 1) })
        {
            var rules = (await store.GetAsOfAsync<GstReturnDueDates>(day, TestContext.Current.CancellationToken))!.Rules;

            var found = Assert.Single(rules, candidate => candidate.Rule == rule);
            Assert.Equal((form, filers, period, opensDay, dueDay, monthsAfter), (found.Form, found.Filers, found.Period, found.OpensDay, found.DueDay, found.MonthsAfter));
        }

        Assert.Contains(instrument, await SourceAsync(scope, "gst.return-due-dates", rule), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("27", "Maharashtra", "gstr-3b-quarterly-1")]
    [InlineData("29", "Karnataka", "gstr-3b-quarterly-1")]
    [InlineData("31", "Lakshadweep", "gstr-3b-quarterly-1")]
    [InlineData("07", "Delhi", "gstr-3b-quarterly-2")]
    [InlineData("01", "Jammu and Kashmir", "gstr-3b-quarterly-2")]
    [InlineData("38", "Ladakh", "gstr-3b-quarterly-2")]
    [InlineData("04", "Chandigarh", "gstr-3b-quarterly-2")]
    public async Task StateList_OfAState_IsTheOneRule61NamesItIn(string stateCode, string listedAs, string rule)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var states = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<QuarterlyReturnStates>(FirstPeriod, TestContext.Current.CancellationToken))!.States;

        Assert.Equal(new QuarterlyReturnState(stateCode, listedAs, rule), Assert.Single(states, state => state.StateCode == stateCode));
        Assert.Contains("rule 61(1)(ii), Table", await SourceAsync(scope, "gst.quarterly-return-states", stateCode), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StateLists_HoldThirteenAndTwentyTwoStatesAndNeitherTheMergedTerritoryNorTheOtherTerritory()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var states = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<QuarterlyReturnStates>(new DateOnly(2026, 11, 1), TestContext.Current.CancellationToken))!.States;

        Assert.Equal(35, states.Count);
        Assert.Equal(13, states.Count(state => state.Rule == "gstr-3b-quarterly-1"));
        Assert.Equal(22, states.Count(state => state.Rule == "gstr-3b-quarterly-2"));
        Assert.DoesNotContain(states, state => state.StateCode is "25" or "26" or "97");
    }

    [Theory]
    [InlineData("qrmp-eligibility", "50000000", "Notification No. 84/2020-Central Tax")]
    [InlineData("qrmp-exit", "50000000", "rule 61A(2)")]
    [InlineData("iff-monthly-value", "5000000", "rule 59(2), as substituted by Notification No. 82/2020-Central Tax")]
    [InlineData("gstr-9c", "50000000", "rule 80(3), as substituted by Notification No. 30/2021-Central Tax")]
    public async Task Limit_OfEachTest_IsTheAmountItsInstrumentSets(string threshold, string rupees, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var thresholds = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<GstReturnThresholds>(FirstPeriod, TestContext.Current.CancellationToken))!.Thresholds;

        Assert.Equal(new Money(decimal.Parse(rupees, CultureInfo.InvariantCulture), Currency.Inr), Assert.Single(thresholds, candidate => candidate.Threshold == threshold).Amount);
        Assert.Contains(instrument, await SourceAsync(scope, "gst.return-thresholds", threshold), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Limits_OfEveryVersion_HoldNoGstr9Exemption()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var history = scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>();

        var set = await history.GetSetAsync("gst.return-thresholds", new DateOnly(2026, 11, 7), TestContext.Current.CancellationToken);

        Assert.Equal([FirstPeriod], set!.Versions.Select(version => version.From));
        foreach (var version in set.Versions)
        {
            var view = await history.GetVersionAsync("gst.return-thresholds", version.From, TestContext.Current.CancellationToken);
            Assert.Equal(["gstr-9c", "iff-monthly-value", "qrmp-eligibility", "qrmp-exit"], view!.Rows.Select(row => row.Key));
            Assert.DoesNotContain(view.Rows, row => row.Key.Contains("exemption", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task ReturnSets_OnTheDayBeforeTheirFirstTaxPeriod_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var day = new DateOnly(2020, 12, 31);

        Assert.Null(await store.GetAsOfAsync<GstReturnDueDates>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<QuarterlyReturnStates>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<GstReturnThresholds>(day, TestContext.Current.CancellationToken));
    }

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, string rowKey)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, FirstPeriod, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
