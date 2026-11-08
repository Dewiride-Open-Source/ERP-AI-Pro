using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Numbering;

public sealed class NumberingAdministrationTests
{
    private static readonly NumberFormat Format = new("TI.", NumberYearStyle.Full, ".", 3);

    private static readonly NumberFormat Changed = new("TX", NumberYearStyle.Abbreviated, "-", 4);

    [Fact]
    public async Task List_AKindNotSetUp_HasTheYearsRuleAndNoNumbering()
    {
        await using var database = await NumberingDatabase.CreateAsync();

        var documents = await database.AdministerAsync((administration, token) => administration.ListAsync(token));

        var listed = Assert.Single(documents);
        Assert.Equal((database.Document, NumberingDatabase.CurrentYear, NumberingDatabase.Rule), (listed.Document, listed.CurrentYear, listed.Rule));
        Assert.Null(listed.Series);
    }

    [Fact]
    public async Task SetUp_AFormatTheRuleAllows_OpensTheCurrentYearAtItsNextNumberAndRecordsBoth()
    {
        await using var database = await NumberingDatabase.CreateAsync();

        var setUp = await database.AdministerAsync((administration, token) => administration.SetUpAsync(database.Document.Key, Format, 38, token));

        var series = setUp.Value.Series!;
        Assert.Equal(Format, series.Format);
        var year = Assert.Single(series.Years);
        Assert.Equal(
            (NumberingDatabase.CurrentYear, 38L, 38L, 999L, false, "TI.2026-27.038", "TI.2026-27.038", "TI.2026-27.999", (string?)null),
            (year.Year, year.FirstNumber, year.NextNumber, year.LastNumber, year.InUse, year.FirstText, year.NextText, year.LastText, year.LastIssuedText));
        Assert.Equal(ChangeOperation.Created, Assert.Single(await database.ChangeRecordsOfAsync(series.Id.Value)).Operation);
    }

    [Fact]
    public async Task SetUp_Twice_IsRefused()
    {
        await using var database = await SetUpAsync();

        var again = await database.AdministerAsync((administration, token) => administration.SetUpAsync(database.Document.Key, Format, 1, token));

        Assert.Equal(("numbering.already-set-up", ErrorKind.Conflict), (again.Error!.Code, again.Error.Kind));
    }

    [Fact]
    public async Task SetUp_AFormatTheRuleRefuses_AnswersTheFieldsProblem()
    {
        await using var database = await NumberingDatabase.CreateAsync();

        var refused = await database.AdministerAsync((administration, token) => administration.SetUpAsync(database.Document.Key, Format with { Prefix = "ti." }, 1, token));

        Assert.Equal(("numbering.format-invalid", ErrorKind.Validation), (refused.Error!.Code, refused.Error.Kind));
        Assert.Equal([NumberFormat.PrefixField], refused.Error.Fields.Keys);
    }

    [Fact]
    public async Task SetUp_ANextNumberPastTheLastThatFits_IsRefused()
    {
        await using var database = await NumberingDatabase.CreateAsync();

        var refused = await database.AdministerAsync((administration, token) => administration.SetUpAsync(database.Document.Key, Format, 1000, token));

        Assert.Equal("numbering.next-number-out-of-range", refused.Error!.Code);
        Assert.Equal([NumberingErrors.NextNumberField], refused.Error.Fields.Keys);
    }

    [Fact]
    public async Task SetUp_AKindNoModuleDeclares_IsRefused()
    {
        await using var database = await NumberingDatabase.CreateAsync();

        var refused = await database.AdministerAsync((administration, token) => administration.SetUpAsync("test.numbering.undeclared", Format, 1, token));

        Assert.Equal("numbering.document-unknown", refused.Error!.Code);
        Assert.Equal([NumberingErrors.DocumentTypeField], refused.Error.Fields.Keys);
    }

    [Fact]
    public async Task SetUp_AYearNoRuleCovers_IsRefused()
    {
        await using var database = await NumberingDatabase.CreateAsync();
        database.Rules.Current = null;

        var refused = await database.AdministerAsync((administration, token) => administration.SetUpAsync(database.Document.Key, Format, 1, token));

        Assert.Equal("numbering.no-rule", refused.Error!.Code);
    }

    [Fact]
    public async Task Change_BeforeTheYearGivesANumber_ReformatsTheYearAtItsNewNextNumber()
    {
        await using var database = await SetUpAsync();
        var details = await ListedAsync(database);

        var changed = await database.AdministerAsync((administration, token) => administration.ChangeAsync(details.Series!.Id, Version(details), Changed, 5, token));
        var issued = await database.IssueAsync(NumberingDatabase.Today);

        Assert.Equal(Changed, changed.Value.Series!.Format);
        Assert.Equal("TX26-27-0005", Assert.Single(changed.Value.Series.Years).FirstText);
        Assert.Equal("TX26-27-0005", issued.Value.Text);
    }

    [Fact]
    public async Task Change_KeepingTheNextNumber_KeepsTheYearsFirstNumber()
    {
        await using var database = await SetUpAsync(nextNumber: 12);
        var details = await ListedAsync(database);

        var changed = await database.AdministerAsync((administration, token) => administration.ChangeAsync(details.Series!.Id, Version(details), Changed, null, token));

        Assert.Equal("TX26-27-0012", Assert.Single(changed.Value.Series!.Years).FirstText);
    }

    [Fact]
    public async Task Change_AfterTheYearGaveANumber_KeepsTheYearsFormatAndAppliesFromTheNextYear()
    {
        await using var database = await SetUpAsync();
        await database.IssueAsync(NumberingDatabase.Today);
        var details = await ListedAsync(database);

        var changed = await database.AdministerAsync((administration, token) => administration.ChangeAsync(details.Series!.Id, Version(details), Changed, 2, token));
        var thisYear = await database.IssueAsync(NumberingDatabase.Today);
        var nextYear = await database.IssueAsync(new DateOnly(2027, 4, 1));

        Assert.True(changed.IsSuccess);
        Assert.Equal(Changed, changed.Value.Series!.Format);
        var year = Assert.Single(changed.Value.Series.Years);
        Assert.Equal((Format, true, "TI.2026-27.001"), (year.Format, year.InUse, year.LastIssuedText));
        Assert.Equal("TI.2026-27.002", thisYear.Value.Text);
        Assert.Equal("TX27-28-0001", nextYear.Value.Text);
    }

    [Fact]
    public async Task Change_ANewNextNumberOnceTheYearGaveOne_IsRefused()
    {
        await using var database = await SetUpAsync();
        await database.IssueAsync(NumberingDatabase.Today);
        var details = await ListedAsync(database);

        var refused = await database.AdministerAsync((administration, token) => administration.ChangeAsync(details.Series!.Id, Version(details), Format, 7, token));

        Assert.Equal(("numbering.year-in-use", ErrorKind.Validation), (refused.Error!.Code, refused.Error.Kind));
        Assert.Equal([NumberingErrors.NextNumberField], refused.Error.Fields.Keys);
    }

    [Fact]
    public async Task Change_FromAVersionAChangeReplaced_IsRefused()
    {
        await using var database = await SetUpAsync();
        var details = await ListedAsync(database);
        var first = await database.AdministerAsync((administration, token) => administration.ChangeAsync(details.Series!.Id, Version(details), Changed, null, token));

        var stale = await database.AdministerAsync((administration, token) => administration.ChangeAsync(details.Series!.Id, Version(details), Format, null, token));

        Assert.True(first.IsSuccess);
        Assert.Equal((EfUnitOfWork.ConcurrencyConflictCode, ErrorKind.Conflict), (stale.Error!.Code, stale.Error.Kind));
    }

    [Fact]
    public async Task Change_OfTheNextNumberAloneFromAVersionAChangeReplaced_IsRefused()
    {
        await using var database = await SetUpAsync();
        var details = await ListedAsync(database);
        var first = await database.AdministerAsync((administration, token) => administration.ChangeAsync(details.Series!.Id, Version(details), Format, 5, token));

        var stale = await database.AdministerAsync((administration, token) => administration.ChangeAsync(details.Series!.Id, Version(details), Format, 9, token));

        Assert.NotEqual(details.Series!.Version, first.Value.Series!.Version);
        Assert.Equal((EfUnitOfWork.ConcurrencyConflictCode, ErrorKind.Conflict), (stale.Error!.Code, stale.Error.Kind));
        Assert.Equal(5, Assert.Single((await ListedAsync(database)).Series!.Years).NextNumber);
    }

    [Fact]
    public async Task Change_KeepingTheFormatAndNextNumber_WritesNoChangeRecord()
    {
        await using var database = await SetUpAsync();
        var details = await ListedAsync(database);

        var unchanged = await database.AdministerAsync((administration, token) => administration.ChangeAsync(details.Series!.Id, Version(details), Format, null, token));

        Assert.True(unchanged.IsSuccess);
        Assert.Equal([ChangeOperation.Created], (await database.ChangeRecordsOfAsync(details.Series!.Id.Value)).Select(record => record.Operation));
    }

    [Fact]
    public async Task Change_AnIdNoNumberingHas_IsNotFound()
    {
        await using var database = await NumberingDatabase.CreateAsync();

        var missing = await database.AdministerAsync((administration, token) => administration.ChangeAsync(NumberSeriesId.Create(), new byte[8], Format, null, token));

        Assert.Equal(("numbering.series-not-found", ErrorKind.NotFound), (missing.Error!.Code, missing.Error.Kind));
    }

    [Fact]
    public async Task Get_ANumbering_DescribesItsKindAndYears()
    {
        await using var database = await SetUpAsync();
        await database.IssueAsync(NumberingDatabase.Today);
        var details = await ListedAsync(database);

        var read = await database.AdministerAsync((administration, token) => administration.GetAsync(details.Series!.Id, token));

        Assert.Equal(database.Document, read.Value.Document);
        Assert.Equal("TI.2026-27.001", Assert.Single(read.Value.Series!.Years).LastIssuedText);
        Assert.Equal("numbering.series-not-found", (await database.AdministerAsync((administration, token) => administration.GetAsync(NumberSeriesId.Create(), token))).Error!.Code);
    }

    [Fact]
    public async Task Names_OfTheNumberingAndItsYear_AreItsKindsTitleAndTheYear()
    {
        await using var database = await SetUpAsync();
        var details = await ListedAsync(database);
        var year = await database.QueryAsync((context, token) => context.Years.AsNoTracking().SingleAsync(y => y.SeriesId == details.Series!.Id, token));

        var names = await database.QueryAsync((context, token) => new NumberingRecordNames(context, new([new(database.Document)])).NamesAsync(NumberingTables.SeriesChangeKey, [details.Series!.Id.Value], token));
        var yearNames = await database.QueryAsync((context, token) => new NumberingRecordNames(context, new([new(database.Document)])).NamesAsync(NumberingTables.YearsChangeKey, [year.Id.Value], token));

        Assert.Equal("Test invoices", names[details.Series!.Id.Value]);
        Assert.Equal("Test invoices 2026-27", yearNames[year.Id.Value]);
    }

    private static byte[] Version(NumberedDocumentDetails details) =>
        RecordVersion.TryDecode(details.Series!.Version, out var version) ? version : throw new InvalidOperationException("The listed version is not a record version.");

    private static async Task<NumberedDocumentDetails> ListedAsync(NumberingDatabase database) =>
        Assert.Single(await database.AdministerAsync((administration, token) => administration.ListAsync(token)));

    private static async Task<NumberingDatabase> SetUpAsync(long nextNumber = 1)
    {
        var database = await NumberingDatabase.CreateAsync();
        var setUp = await database.AdministerAsync((administration, token) => administration.SetUpAsync(database.Document.Key, Format, nextNumber, token));
        Assert.True(setUp.IsSuccess, setUp.Error?.Message);

        return database;
    }
}
