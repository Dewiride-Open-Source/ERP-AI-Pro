using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Numbering.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Numbering;

public sealed class DocumentNumberTests
{
    private static readonly NumberFormat Format = new("TI.", NumberYearStyle.Full, ".", 3);

    [Fact]
    public async Task NextAsync_SuccessiveIssues_GivesConsecutiveNumbersFromTheFirst()
    {
        await using var database = await SetUpAsync();

        var numbers = new List<string>();
        for (var issue = 0; issue < 3; issue++)
        {
            numbers.Add((await database.IssueAsync(NumberingDatabase.Today)).Value.Text);
        }

        Assert.Equal(["TI.2026-27.001", "TI.2026-27.002", "TI.2026-27.003"], numbers);
        Assert.Equal([1L, 2L, 3L], (await database.InvoicesAsync()).Select(invoice => invoice.Sequence));
    }

    [Fact]
    public async Task NextAsync_ManyIssuesAtOnceSomeRolledBack_CommitsEveryNumberFromOneWithoutAGapOrRepeat()
    {
        const int issues = 40;
        await using var database = await SetUpAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, issues).Select(index => database.IssueAsync(NumberingDatabase.Today, rollBack: index % 4 == 3)));

        Assert.All(outcomes, outcome => Assert.True(outcome.IsSuccess));
        var committed = await database.InvoicesAsync();
        Assert.Equal(issues - (issues / 4), committed.Count);
        Assert.Equal(Enumerable.Range(1, committed.Count).Select(sequence => (long)sequence), committed.Select(invoice => invoice.Sequence));
        Assert.Equal(committed.Count, committed.Select(invoice => invoice.Number).Distinct(StringComparer.Ordinal).Count());
        var year = await database.QueryAsync((context, token) => context.Years.AsNoTracking().SingleAsync(y => y.Year == NumberingDatabase.CurrentYear && context.Series.Any(s => s.Id == y.SeriesId && s.DocumentType == database.Document.Key), token));
        Assert.Equal(committed.Count + 1L, year.NextNumber);
    }

    [Fact]
    public async Task NextAsync_InAUnitOfWorkThatFails_GivesItsNumberToTheNextIssue()
    {
        await using var database = await SetUpAsync();

        var failed = await database.RunUnitAsync(async (context, numbers, token) =>
        {
            var number = await numbers.NextAsync(context, database.Document.Key, NumberingDatabase.Today, token);
            Assert.Equal(1, number.Value.Sequence);
            context.Invoices.Add(SampleInvoice.Issue(database.Document.Key, number.Value));

            return Error.Failure("test.refused", "The issue is refused after its number was taken.");
        });
        var next = await database.IssueAsync(NumberingDatabase.Today);

        Assert.Equal("test.refused", failed.Error!.Code);
        Assert.Equal("TI.2026-27.001", next.Value.Text);
        Assert.Single(await database.InvoicesAsync());
    }

    [Fact]
    public async Task NextAsync_ADocumentOfTheNextFinancialYear_StartsThatYearAtOneInItsFormat()
    {
        await using var database = await SetUpAsync();
        await database.IssueAsync(NumberingDatabase.Today);
        await database.IssueAsync(NumberingDatabase.Today);

        var nextYear = await database.IssueAsync(new DateOnly(2027, 4, 1));
        var lastDay = await database.IssueAsync(new DateOnly(2027, 3, 31));

        Assert.Equal(("TI.2027-28.001", 1L), (nextYear.Value.Text, nextYear.Value.Sequence));
        Assert.Equal("TI.2026-27.003", lastDay.Value.Text);
    }

    [Fact]
    public async Task NextAsync_AKindNotSetUp_IsRefusedAndSavesNothing()
    {
        await using var database = await NumberingDatabase.CreateAsync();

        var refused = await database.IssueAsync(NumberingDatabase.Today);

        Assert.Equal(("numbering.not-set-up", ErrorKind.Failure), (refused.Error!.Code, refused.Error.Kind));
        Assert.Empty(await database.InvoicesAsync());
    }

    [Fact]
    public async Task NextAsync_AYearPastItsLastNumber_IsRefused()
    {
        var last = Format.LastNumberWithin(NumberingDatabase.Rule.MaxLength);
        await using var database = await SetUpAsync(nextNumber: last);

        var final = await database.IssueAsync(NumberingDatabase.Today);
        var refused = await database.IssueAsync(NumberingDatabase.Today);

        Assert.Equal(last, final.Value.Sequence);
        Assert.Equal(NumberingDatabase.Rule.MaxLength, final.Value.Text.Length);
        Assert.Equal(("numbering.exhausted", ErrorKind.Failure), (refused.Error!.Code, refused.Error.Kind));
    }

    [Fact]
    public async Task NextAsync_AYearNoRuleCovers_IsRefusedWhileAnOpenYearStillNumbers()
    {
        await using var database = await SetUpAsync();
        database.Rules.Current = null;

        var refused = await database.IssueAsync(new DateOnly(2027, 4, 1));
        var opened = await database.IssueAsync(NumberingDatabase.Today);

        Assert.Equal("numbering.no-rule", refused.Error!.Code);
        Assert.Equal("TI.2026-27.001", opened.Value.Text);
    }

    [Fact]
    public async Task NextAsync_AYearWhoseRuleRefusesTheFormat_IsRefused()
    {
        await using var database = await SetUpAsync();
        database.Rules.Current = NumberingDatabase.Rule with { MaxLength = Format.FixedLength };

        var refused = await database.IssueAsync(new DateOnly(2027, 4, 1));

        Assert.Equal("numbering.format-not-allowed", refused.Error!.Code);
    }

    [Fact]
    public async Task NextAsync_OpeningAYear_WritesOneChangeRecordAndTheNumbersGivenWriteNone()
    {
        await using var database = await SetUpAsync();

        for (var issue = 0; issue < 3; issue++)
        {
            await database.IssueAsync(new DateOnly(2027, 5, 1));
        }

        var opened = await database.QueryAsync((context, token) => context.Years.AsNoTracking().SingleAsync(y => y.Year == NumberingDatabase.CurrentYear.Next && context.Series.Any(s => s.Id == y.SeriesId && s.DocumentType == database.Document.Key), token));
        var record = Assert.Single(await database.ChangeRecordsOfAsync(opened.Id.Value));
        Assert.Equal((ChangeOperation.Created, NumberingTables.YearsChangeKey, database.Actor.ActorId), (record.Operation, record.EntityType, record.ActorId));
        Assert.Equal(4, opened.NextNumber);
    }

    [Fact]
    public async Task NextAsync_OutsideATransaction_Throws()
    {
        await using var database = await SetUpAsync();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => database.RunWithoutTransactionAsync());

        Assert.Contains("outside a unit of work", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NextAsync_AContextThatMapsNoNumbering_Throws()
    {
        await using var database = await SetUpAsync();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => database.RunOnContextAsync<AuditingDbContext>());

        Assert.Contains("maps no numbering", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NextAsync_AKindNoModuleDeclares_Throws()
    {
        await using var database = await SetUpAsync();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => database.IssueAsync(NumberingDatabase.Today, documentType: "test.numbering.undeclared"));

        Assert.Contains("is not a numbered document", refusal.Message, StringComparison.Ordinal);
    }

    private static async Task<NumberingDatabase> SetUpAsync(long nextNumber = 1)
    {
        var database = await NumberingDatabase.CreateAsync();
        var setUp = await database.AdministerAsync((administration, token) => administration.SetUpAsync(database.Document.Key, Format, nextNumber, token));
        Assert.True(setUp.IsSuccess, setUp.Error?.Message);

        return database;
    }
}
