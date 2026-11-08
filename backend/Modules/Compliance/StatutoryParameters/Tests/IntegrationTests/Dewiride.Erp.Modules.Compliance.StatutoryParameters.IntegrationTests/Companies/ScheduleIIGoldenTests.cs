using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Companies.ScheduleII;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Companies;

// Golden values of Schedule II to the Companies Act, 2013 for financial years from 2026-27: every useful life of Part C with its
// extra-shift mark, and the residual value and shift limits of Part A and the Notes, each pinned beside its instrument so that a
// wrong edit of a seed file fails a test (ADR-0063). The sets claim no financial year before 2026-27, and continuous process
// plant has no life in them.
[Collection(StatutoryRows.Name)]
public sealed class ScheduleIIGoldenTests(ErpApiFactory factory)
{
    private const string UsefulLivesKey = "companies.schedule-ii-useful-lives";

    private const string LimitsKey = "companies.schedule-ii-limits";

    private const string ActForTheLives = "Companies Act, 2013 (18 of 2013), Gazette of India, Extraordinary, Part II, Section 1, No. 27, 30 August 2013: Schedule II, Part C and Notes, paragraph 6, in force 1 April 2014, read with section 123(2)";

    private const string NoteToTheNotification1075 = "Notification G.S.R. 1075(E), 17 November 2016, Ministry of Corporate Affairs, Note: Schedule II came into force on 1 April 2014 and was amended by G.S.R. 237(E), 31 March 2014, with effect from 1 April 2014, and by G.S.R. 627(E), 29 August 2014";

    private const string Notification627 = "Notification G.S.R. 627(E), 29 August 2014, Ministry of Corporate Affairs, paragraph 1(a), substituting Schedule II, Part A, paragraph 3(i), in force on its publication in the Gazette of India, Extraordinary, Part II, Section 3, Sub-section (i), No. 456, 29 August 2014";

    private const string ActForTheShifts = "Companies Act, 2013 (18 of 2013), Gazette of India, Extraordinary, Part II, Section 1, No. 27, 30 August 2013: Schedule II, Notes, paragraph 6, in force 1 April 2014";

    private const string Paragraph6 = "Companies Act, 2013 (18 of 2013), Schedule II, Notes, paragraph 6";

    private static readonly DateOnly FirstFinancialYear = new(2026, 4, 1);

    [Theory]
    [InlineData("I(a)", 60, ExtraShift.Nesd)]
    [InlineData("I(b)", 30, ExtraShift.Nesd)]
    [InlineData("I(c)", 30, ExtraShift.Nesd)]
    [InlineData("I(d)", 5, ExtraShift.Nesd)]
    [InlineData("I(e)", 3, ExtraShift.Nesd)]
    [InlineData("II", 30, ExtraShift.Nesd)]
    [InlineData("III(a)(i)", 10, ExtraShift.Nesd)]
    [InlineData("III(a)(ii)", 5, ExtraShift.Nesd)]
    [InlineData("III(b)", 3, ExtraShift.Nesd)]
    [InlineData("IV(i)(a)", 15, ExtraShift.Permitted)]
    [InlineData("IV(ii)(a)(1)", 13, ExtraShift.Permitted)]
    [InlineData("IV(ii)(a)(2)", 13, ExtraShift.Permitted)]
    [InlineData("IV(ii)(b)(1)", 13, ExtraShift.Permitted)]
    [InlineData("IV(ii)(b)(2)", 8, ExtraShift.Nesd)]
    [InlineData("IV(ii)(b)(3)", 10, ExtraShift.Nesd)]
    [InlineData("IV(ii)(c)", 8, ExtraShift.Nesd)]
    [InlineData("IV(ii)(d)(1)", 18, ExtraShift.Nesd)]
    [InlineData("IV(ii)(d)(2)", 13, ExtraShift.Nesd)]
    [InlineData("IV(ii)(d)(3)", 18, ExtraShift.Nesd)]
    [InlineData("IV(ii)(d)(4)", 18, ExtraShift.Nesd)]
    [InlineData("IV(ii)(e)(1)", 25, ExtraShift.Nesd)]
    [InlineData("IV(ii)(e)(2)", 25, ExtraShift.Nesd)]
    [InlineData("IV(ii)(e)(3)", 25, ExtraShift.Nesd)]
    [InlineData("IV(ii)(e)(4)", 25, ExtraShift.Nesd)]
    [InlineData("IV(ii)(e)(5)", 30, ExtraShift.Nesd)]
    [InlineData("IV(ii)(e)(6)", 30, ExtraShift.Nesd)]
    [InlineData("IV(ii)(e)(7)", 8, ExtraShift.Nesd)]
    [InlineData("IV(ii)(e)(8)", 8, ExtraShift.Nesd)]
    [InlineData("IV(ii)(f)(1)", 40, ExtraShift.Nesd)]
    [InlineData("IV(ii)(f)(2)", 40, ExtraShift.Nesd)]
    [InlineData("IV(ii)(f)(3)", 40, ExtraShift.Nesd)]
    [InlineData("IV(ii)(f)(4)", 40, ExtraShift.Nesd)]
    [InlineData("IV(ii)(f)(5)", 22, ExtraShift.Nesd)]
    [InlineData("IV(ii)(f)(6)", 35, ExtraShift.Nesd)]
    [InlineData("IV(ii)(f)(7)", 30, ExtraShift.Nesd)]
    [InlineData("IV(ii)(f)(8)", 30, ExtraShift.Nesd)]
    [InlineData("IV(ii)(g)(1)", 20, ExtraShift.Permitted)]
    [InlineData("IV(ii)(g)(2)", 20, ExtraShift.Permitted)]
    [InlineData("IV(ii)(g)(3)", 20, ExtraShift.Permitted)]
    [InlineData("IV(ii)(g)(4)", 20, ExtraShift.Permitted)]
    [InlineData("IV(ii)(g)(5)", 25, ExtraShift.Permitted)]
    [InlineData("IV(ii)(h)(1)", 40, ExtraShift.Nesd)]
    [InlineData("IV(ii)(h)(2)", 40, ExtraShift.Nesd)]
    [InlineData("IV(ii)(h)(3)", 40, ExtraShift.Nesd)]
    [InlineData("IV(ii)(h)(4)", 40, ExtraShift.Nesd)]
    [InlineData("IV(ii)(h)(5)", 40, ExtraShift.Nesd)]
    [InlineData("IV(ii)(h)(6)", 40, ExtraShift.Nesd)]
    [InlineData("IV(ii)(h)(7)", 40, ExtraShift.Permitted)]
    [InlineData("IV(ii)(h)(8)", 30, ExtraShift.Permitted)]
    [InlineData("IV(ii)(h)(9)", 30, ExtraShift.Permitted)]
    [InlineData("IV(ii)(h)(10)", 30, ExtraShift.Permitted)]
    [InlineData("IV(ii)(h)(11)", 30, ExtraShift.Nesd)]
    [InlineData("IV(ii)(h)(12)", 25, ExtraShift.Permitted)]
    [InlineData("IV(ii)(h)(13)", 25, ExtraShift.Nesd)]
    [InlineData("IV(ii)(i)(1)", 13, ExtraShift.Nesd)]
    [InlineData("IV(ii)(i)(2)", 15, ExtraShift.Nesd)]
    [InlineData("IV(ii)(j)(1)", 20, ExtraShift.Nesd)]
    [InlineData("IV(ii)(j)(2)", 20, ExtraShift.Nesd)]
    [InlineData("IV(ii)(j)(3)", 20, ExtraShift.Nesd)]
    [InlineData("IV(ii)(j)(4)", 20, ExtraShift.Nesd)]
    [InlineData("IV(ii)(k)(1)", 12, ExtraShift.Permitted)]
    [InlineData("IV(ii)(k)(2)-more-than-100-tons", 20, ExtraShift.Permitted)]
    [InlineData("IV(ii)(k)(2)-less-than-100-tons", 15, ExtraShift.Permitted)]
    [InlineData("IV(ii)(k)(3)", 10, ExtraShift.Nesd)]
    [InlineData("IV(ii)(k)(4)", 9, ExtraShift.Permitted)]
    [InlineData("IV(ii)(k)(5)", 12, ExtraShift.Nesd)]
    [InlineData("IV(ii)(l)", 15, ExtraShift.Nesd)]
    [InlineData("V(i)", 10, ExtraShift.Nesd)]
    [InlineData("V(ii)", 8, ExtraShift.Nesd)]
    [InlineData("VI(1)", 10, ExtraShift.Nesd)]
    [InlineData("VI(2)", 6, ExtraShift.Nesd)]
    [InlineData("VI(3)", 8, ExtraShift.Nesd)]
    [InlineData("VI(4)", 8, ExtraShift.Nesd)]
    [InlineData("VI(5)", 8, ExtraShift.Nesd)]
    [InlineData("VII(1)(i)", 25, ExtraShift.Nesd)]
    [InlineData("VII(1)(ii)", 20, ExtraShift.Nesd)]
    [InlineData("VII(1)(iii)(a)", 25, ExtraShift.Nesd)]
    [InlineData("VII(1)(iii)(b)", 20, ExtraShift.Nesd)]
    [InlineData("VII(1)(iv)", 30, ExtraShift.Nesd)]
    [InlineData("VII(1)(v)", 30, ExtraShift.Nesd)]
    [InlineData("VII(1)(vi)", 30, ExtraShift.Nesd)]
    [InlineData("VII(1)(vii)", 20, ExtraShift.Nesd)]
    [InlineData("VII(1)(viii)", 20, ExtraShift.Nesd)]
    [InlineData("VII(1)(ix)", 25, ExtraShift.Nesd)]
    [InlineData("VII(1)(x)", 15, ExtraShift.Nesd)]
    [InlineData("VII(1)(xi)", 10, ExtraShift.Nesd)]
    [InlineData("VII(1)(xii)", 14, ExtraShift.Nesd)]
    [InlineData("VII(2)(i)", 13, ExtraShift.Nesd)]
    [InlineData("VII(2)(ii)", 28, ExtraShift.Nesd)]
    [InlineData("VIII", 20, ExtraShift.Nesd)]
    [InlineData("IX", 15, ExtraShift.Nesd)]
    [InlineData("X", 15, ExtraShift.Nesd)]
    [InlineData("XI", 5, ExtraShift.Nesd)]
    [InlineData("XII(i)", 6, ExtraShift.Nesd)]
    [InlineData("XII(ii)", 3, ExtraShift.Nesd)]
    [InlineData("XIII(i)", 10, ExtraShift.Nesd)]
    [InlineData("XIII(ii)", 5, ExtraShift.Nesd)]
    [InlineData("XIV", 10, ExtraShift.Nesd)]
    [InlineData("XV", 15, ExtraShift.Nesd)]
    public async Task UsefulLife_OfEachEntryOfPartC_IsTheLifeAndExtraShiftMarkTheScheduleGivesIt(string entry, int years, ExtraShift extraShift)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var lives = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<UsefulLives>(FirstFinancialYear, TestContext.Current.CancellationToken))!.Lives;

        var found = Assert.Single(lives, candidate => candidate.Entry == entry);
        Assert.Equal((years, extraShift), (found.Years, found.ExtraShift));
        Assert.StartsWith("Companies Act, 2013 (18 of 2013), Schedule II, Part C, paragraph 5, entry ", await SourceAsync(scope, UsefulLivesKey, entry), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UsefulLives_OfTheFirstFinancialYear_HoldNinetyNineEntriesAndNoContinuousProcessPlant()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var lives = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<UsefulLives>(FirstFinancialYear, TestContext.Current.CancellationToken))!.Lives;

        Assert.Equal(99, lives.Count);
        Assert.DoesNotContain(lives, life => life.Entry.StartsWith("IV(i)(b)", StringComparison.Ordinal));
        Assert.Equal(81, lives.Count(life => life.ExtraShift == ExtraShift.Nesd));
        Assert.Equal(18, lives.Count(life => life.ExtraShift == ExtraShift.Permitted));
    }

    [Theory]
    [InlineData("residual-value", 5, "the residual value of an asset shall not be more than five per cent. of the original cost of the asset", "Companies Act, 2013 (18 of 2013), Schedule II, Part A, paragraph 3(i), as substituted by Notification G.S.R. 627(E), 29 August 2014, paragraph 1(a)")]
    [InlineData("double-shift", 50, "for double shift, the depreciation will increase by 50% for that period", Paragraph6)]
    [InlineData("triple-shift", 100, "in case of the triple shift the depreciation shall be calculated on the basis of 100% for that period.", Paragraph6)]
    public async Task Limit_OfEachTest_IsThePercentageTheScheduleSets(string limit, int percent, string words, string source)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var limits = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ScheduleIILimits>(FirstFinancialYear, TestContext.Current.CancellationToken))!.Limits;

        var found = Assert.Single(limits, candidate => candidate.Limit == limit);
        Assert.Equal(new Percentage(percent), found.Percent);
        Assert.Contains(words, found.Test, StringComparison.Ordinal);
        Assert.Equal(source, await SourceAsync(scope, LimitsKey, limit));
    }

    [Theory]
    [InlineData(UsefulLivesKey, 99, new[] { ActForTheLives, NoteToTheNotification1075 })]
    [InlineData(LimitsKey, 3, new[] { Notification627, ActForTheShifts })]
    public async Task Version_OfEachSet_IsTheFirstFinancialYearAloneAndAddsEveryEntry(string key, int entries, string[] instruments)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, new DateOnly(2026, 11, 8), TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((ParameterSetBasis.Law, ParameterAsOf.FinancialYearStart), (set.Basis, set.AsOf));
        var version = Assert.Single(set.Versions);
        Assert.Equal((FirstFinancialYear, (DateOnly?)null, entries, 0, 0), (version.From, version.Until, version.AddedCount, version.ChangedCount, version.RemovedCount));
        Assert.Equal(instruments, version.Instruments);
    }

    [Theory]
    [InlineData("2014-04-01")]
    [InlineData("2026-03-31")]
    public async Task Sets_BeforeTheFirstFinancialYear_HoldNothing(string day)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var date = DateOnly.Parse(day, CultureInfo.InvariantCulture);

        Assert.Null(await store.GetAsOfAsync<UsefulLives>(date, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<ScheduleIILimits>(date, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sets_InTheNextFinancialYear_AreStillTheFirstVersionWithNoEnd()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var day = new DateOnly(2027, 4, 1);

        var lives = await store.GetAsOfAsync<UsefulLives>(day, TestContext.Current.CancellationToken);
        var limits = await store.GetAsOfAsync<ScheduleIILimits>(day, TestContext.Current.CancellationToken);

        Assert.Equal((FirstFinancialYear, (DateOnly?)null, 99), (lives!.Version.From, lives.Version.Until, lives.Lives.Count));
        Assert.Equal((FirstFinancialYear, (DateOnly?)null, 3), (limits!.Version.From, limits.Version.Until, limits.Limits.Count));
    }

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, string rowKey)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, FirstFinancialYear, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
