using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Entries;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.IncomeTax.Tds;

// Golden values of the entries of sections 392 and 393 of the Income-tax Act, 2025, each pinned beside its instrument so that a
// wrong edit of a seed file fails a test (ADR-0063). Each case is an entry on the day the Act came into force: the citation the
// Act gives it, the Table it sits in, when its tax is deducted and the provision its source cites.
[Collection(StatutoryRows.Name)]
public sealed class TdsEntryGoldenTests(ErpApiFactory factory)
{
    private static readonly DateOnly ActInForce = new(2026, 4, 1);

    [Theory]
    [InlineData("392(1)", "section 392(1)", TdsTable.Salary, TdsTiming.Payment, "Income-tax Act, 2025, section 392(1), (4) and (8)")]
    [InlineData("392(2)", "section 392(2)", TdsTable.Salary, TdsTiming.Payment, "Income-tax Act, 2025, section 392(2)")]
    [InlineData("392(3)", "section 392(3)", TdsTable.Salary, TdsTiming.Deferred, "Income-tax Act, 2025, section 392(3)")]
    [InlineData("392(6)(a)", "section 392(6)(a)", TdsTable.Salary, TdsTiming.Payment, "Income-tax Act, 2025, section 392(6)(a) and Schedule XI, Part A, paragraphs 9 and 10")]
    [InlineData("392(6)(b)", "section 392(6)(b)", TdsTable.Salary, TdsTiming.Payment, "Income-tax Act, 2025, section 392(6)(b) and Schedule XI, Part B, paragraph 7")]
    [InlineData("392(7)", "section 392(7)", TdsTable.Salary, TdsTiming.Payment, "Income-tax Act, 2025, section 392(7)")]
    [InlineData("393(1)-1(i)", "section 393(1) [Table: Sl. No. 1(i)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 1(i)]")]
    [InlineData("393(1)-1(ii)", "section 393(1) [Table: Sl. No. 1(ii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 1(ii)]")]
    [InlineData("393(1)-2(i)", "section 393(1) [Table: Sl. No. 2(i)]", TdsTable.Residents, TdsTiming.LastMonth, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 2(i)] and Note 1 below serial number 2")]
    [InlineData("393(1)-2(ii)", "section 393(1) [Table: Sl. No. 2(ii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 2(ii)]")]
    [InlineData("393(1)-3(i)", "section 393(1) [Table: Sl. No. 3(i)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Finance Act, 2026, section 84(a)")]
    [InlineData("393(1)-3(ii)", "section 393(1) [Table: Sl. No. 3(ii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 3(ii)] and Note 2 below serial number 3")]
    [InlineData("393(1)-3(iii)", "section 393(1) [Table: Sl. No. 3(iii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 3(iii)]")]
    [InlineData("393(1)-4(i)", "section 393(1) [Table: Sl. No. 4(i)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(i)]")]
    [InlineData("393(1)-4(ii)", "section 393(1) [Table: Sl. No. 4(ii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(ii)]")]
    [InlineData("393(1)-4(iii)", "section 393(1) [Table: Sl. No. 4(iii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(iii)]")]
    [InlineData("393(1)-4(iv)", "section 393(1) [Table: Sl. No. 4(iv)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(iv)]")]
    [InlineData("393(1)-5(i)", "section 393(1) [Table: Sl. No. 5(i)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(i)]")]
    [InlineData("393(1)-5(ii)", "section 393(1) [Table: Sl. No. 5(ii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(ii)] and Notes 1 and 2 below serial number 5")]
    [InlineData("393(1)-5(iii)", "section 393(1) [Table: Sl. No. 5(iii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(iii)] and Notes 1 and 2 below serial number 5")]
    [InlineData("393(1)-6(i)", "section 393(1) [Table: Sl. No. 6(i)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(i)] and the Note below serial number 6")]
    [InlineData("393(1)-6(ii)", "section 393(1) [Table: Sl. No. 6(ii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(ii)]")]
    [InlineData("393(1)-6(iii)", "section 393(1) [Table: Sl. No. 6(iii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(iii)]")]
    [InlineData("393(1)-7", "section 393(1) (Table: Sl. No. 7)", TdsTable.Residents, TdsTiming.BeforeDistribution, "Income-tax Act, 2025, section 393(1) (Table: Sl. No. 7) and the Note below serial number 7")]
    [InlineData("393(1)-8(i)", "section 393(1) [Table: Sl. No. 8(i)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(i)]")]
    [InlineData("393(1)-8(ii)", "section 393(1) [Table: Sl. No. 8(ii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(ii)] and Note 1 below serial number 8")]
    [InlineData("393(1)-8(iii)", "section 393(1) [Table: Sl. No. 8(iii)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(iii)] and Note 5 below serial number 8")]
    [InlineData("393(1)-8(iv)", "section 393(1) [Table: Sl. No. 8(iv)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(iv)] and Notes 2 and 6 below serial number 8")]
    [InlineData("393(1)-8(v)", "section 393(1) [Table: Sl. No. 8(v)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(v)] and Notes 3 and 4 below serial number 8")]
    [InlineData("393(1)-8(vi)", "section 393(1) [Table: Sl. No. 8(vi)]", TdsTable.Residents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(vi)] and Notes 4 and 6 below serial number 8")]
    [InlineData("393(2)-1", "section 393(2) (Table: Sl. No. 1)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 1)")]
    [InlineData("393(2)-2", "section 393(2) (Table: Sl. No. 2)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 2) and Note 1 of the Table")]
    [InlineData("393(2)-3", "section 393(2) (Table: Sl. No. 3)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 3) and Note 1 of the Table")]
    [InlineData("393(2)-4", "section 393(2) (Table: Sl. No. 4)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 4) and Note 1 of the Table")]
    [InlineData("393(2)-5", "section 393(2) (Table: Sl. No. 5)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 5)")]
    [InlineData("393(2)-6", "section 393(2) (Table: Sl. No. 6)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 6)")]
    [InlineData("393(2)-7", "section 393(2) (Table: Sl. No. 7)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 7)")]
    [InlineData("393(2)-8", "section 393(2) (Table: Sl. No. 8)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 8)")]
    [InlineData("393(2)-9", "section 393(2) (Table: Sl. No. 9)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 9)")]
    [InlineData("393(2)-10", "section 393(2) (Table: Sl. No. 10)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 10) and Note 2 of the Table")]
    [InlineData("393(2)-11", "section 393(2) (Table: Sl. No. 11)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 11)")]
    [InlineData("393(2)-12", "section 393(2) (Table: Sl. No. 12)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 12)")]
    [InlineData("393(2)-13", "section 393(2) (Table: Sl. No. 13)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 13)")]
    [InlineData("393(2)-14", "section 393(2) (Table: Sl. No. 14)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 14)")]
    [InlineData("393(2)-15", "section 393(2) (Table: Sl. No. 15)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 15) and Note 2 of the Table")]
    [InlineData("393(2)-16", "section 393(2) (Table: Sl. No. 16)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 16)")]
    [InlineData("393(2)-17", "section 393(2) (Table: Sl. No. 17)", TdsTable.NonResidents, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 17) and Note 3 of the Table")]
    [InlineData("393(3)-1", "section 393(3) (Table: Sl. No. 1)", TdsTable.AnyPerson, TdsTiming.Payment, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 1) and Note 2 of the Table")]
    [InlineData("393(3)-2", "section 393(3) (Table: Sl. No. 2)", TdsTable.AnyPerson, TdsTiming.WithdrawalOrYearEnd, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 2) and Notes 1 and 2 of the Table")]
    [InlineData("393(3)-3", "section 393(3) (Table: Sl. No. 3)", TdsTable.AnyPerson, TdsTiming.Payment, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 3)")]
    [InlineData("393(3)-4", "section 393(3) (Table: Sl. No. 4)", TdsTable.AnyPerson, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 4) and Note 3 of the Table")]
    [InlineData("393(3)-5", "section 393(3) (Table: Sl. No. 5)", TdsTable.AnyPerson, TdsTiming.Payment, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 5)")]
    [InlineData("393(3)-6", "section 393(3) (Table: Sl. No. 6)", TdsTable.AnyPerson, TdsTiming.Payment, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 6)")]
    [InlineData("393(3)-7", "section 393(3) (Table: Sl. No. 7)", TdsTable.AnyPerson, TdsTiming.CreditOrPayment, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 7)")]
    public async Task Entry_OnTheDayTheActCameIntoForce_IsAsTheActPrintsIt(string entry, string citation, TdsTable table, TdsTiming timing, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var entries = await EntriesAsync(scope);

        var found = Assert.Single(entries, candidate => candidate.Entry == entry);
        Assert.Equal((citation, table, timing), (found.Citation, found.Table, found.Timing));
        Assert.Contains(instrument, await SourceAsync(scope, entry), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entries_OnTheDayTheActCameIntoForce_AreTheSixOfSection392AndEverySerialNumberOfTheTablesOfSection393()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var entries = await EntriesAsync(scope);

        Assert.Equal(54, entries.Count);
        Assert.Equal(6, entries.Count(entry => entry.Table == TdsTable.Salary));
        Assert.Equal(24, entries.Count(entry => entry.Table == TdsTable.Residents));
        Assert.Equal(17, entries.Count(entry => entry.Table == TdsTable.NonResidents));
        Assert.Equal(7, entries.Count(entry => entry.Table == TdsTable.AnyPerson));
        Assert.All(entries.Where(entry => entry.Table == TdsTable.NonResidents), entry => Assert.Null(entry.Threshold));
    }

    [Fact]
    public async Task NotesOfTheTransferOfImmovableProperty_OnTheDayTheActCameIntoForce_ReadNote3AsTheFinanceAct2026AmendedIt()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var entries = await EntriesAsync(scope);

        var notes = Assert.Single(entries, entry => entry.Entry == "393(1)-3(i)").Notes;
        Assert.Contains("Note 3.––For the purposes of serial number 3(i), the income-tax shall be deducted", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("serial number 3(iii), the income-tax", notes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TimingOfThePartnersEntry_OnTheDayTheActCameIntoForce_QuotesSection393Sub3cAndTheCreditColumnBNames()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var entries = await EntriesAsync(scope);

        Assert.Equal(
            "at the time of payment thereof in cash or by way of a cheque or a draft or by any other mode, or as specified therein [Table: Sl. No. 7.B] paid to a partner of the firm or credited to his account (including capital account)",
            Assert.Single(entries, entry => entry.Entry == "393(3)-7").TimingWords);
    }

    private static async Task<IReadOnlyList<TdsEntry>> EntriesAsync(AsyncServiceScope scope) =>
        (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsEntries>(ActInForce, TestContext.Current.CancellationToken))?.Entries
        ?? throw new InvalidOperationException($"{TdsEntries.Key} holds nothing for {ActInForce:yyyy-MM-dd}.");

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string entry)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(TdsEntries.Key, ActInForce, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == entry).Source;
    }
}
