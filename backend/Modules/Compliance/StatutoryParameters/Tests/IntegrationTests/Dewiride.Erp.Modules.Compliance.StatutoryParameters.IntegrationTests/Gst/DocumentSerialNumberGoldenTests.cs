using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Documents;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Gst;

// Golden values of the serial-number clauses of the CGST Rules for financial years from 2026-27, each pinned beside its
// instrument so that a wrong edit of the seed file fails a test (ADR-0063). Rule 54's documents and the delivery challan of rule
// 55(1), which names no characters, are not seeded.
[Collection(StatutoryRows.Name)]
public sealed class DocumentSerialNumberGoldenTests(ErpApiFactory factory)
{
    private static readonly DateOnly FirstYear = new(2026, 4, 1);

    [Theory]
    [InlineData("46(b)", "Tax invoice", 16, "-/", "Notification No. 10/2017-Central Tax")]
    [InlineData("49(b)", "Bill of supply", 16, "-/", "Notification No. 10/2017-Central Tax")]
    [InlineData("50(b)", "Receipt voucher", 16, "-/", "Notification No. 10/2017-Central Tax")]
    [InlineData("51(b)", "Refund voucher", 16, "-/", "Notification No. 10/2017-Central Tax")]
    [InlineData("52(b)", "Payment voucher", 16, "-/", "Notification No. 10/2017-Central Tax")]
    [InlineData("53(1)(d)", "Revised tax invoice", 16, "-/", "Notification No. 10/2017-Central Tax")]
    [InlineData("53(1A)(c)", "Credit or debit note", 16, "-/", "Notification No. 3/2019-Central Tax, paragraph 10(d)")]
    public async Task SerialNumber_OfEachDocument_IsTheLengthAndCharactersItsClauseAllows(string rule, string document, int maxLength, string specialCharacters, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        foreach (var day in new[] { FirstYear, new DateOnly(2027, 4, 1) })
        {
            var rules = (await store.GetAsOfAsync<GstDocumentSerialNumbers>(day, TestContext.Current.CancellationToken))!.Rules;

            var found = Assert.Single(rules, candidate => candidate.Rule == rule);
            Assert.Equal((document, maxLength, specialCharacters), (found.Document, found.MaxLength, found.SpecialCharacters));
            Assert.StartsWith("a consecutive serial number not exceeding sixteen characters, in one or multiple series", found.Words, StringComparison.Ordinal);
            Assert.EndsWith("unique for a financial year", found.Words, StringComparison.Ordinal);
        }

        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync("gst.document-serial-numbers", FirstYear, TestContext.Current.CancellationToken);
        Assert.Contains(instrument, Assert.Single(view!.Rows, row => row.Key == rule).Source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SerialNumbers_HoldOneVersionFrom202627WithTheSevenClauses()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var history = scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>();

        var set = await history.GetSetAsync("gst.document-serial-numbers", new DateOnly(2026, 11, 8), TestContext.Current.CancellationToken);
        var view = await history.GetVersionAsync("gst.document-serial-numbers", FirstYear, TestContext.Current.CancellationToken);

        Assert.Equal([FirstYear], set!.Versions.Select(version => version.From));
        Assert.Equal(["46(b)", "49(b)", "50(b)", "51(b)", "52(b)", "53(1)(d)", "53(1A)(c)"], view!.Rows.Select(row => row.Key));
    }

    [Fact]
    public async Task SerialNumbers_BeforeTheFinancialYear202627_AreNotHeld()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.Null(await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<GstDocumentSerialNumbers>(new DateOnly(2025, 4, 1), TestContext.Current.CancellationToken));
    }
}
