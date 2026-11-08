using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.Numbering;

// Kinds of document no release declares, so that a test host lists them on the numbering page. A numbering belongs to its kind and
// every test of a process or of an end-to-end run writes into one database, so each test that changes a numbering takes a kind no
// other test running beside it takes. The odd kinds follow the CGST Rules' clause for a tax invoice, the even ones the clause for
// a credit or debit note.
public static class TestNumberedDocuments
{
    public const int Count = 16;

    public static IReadOnlyList<NumberedDocument> All { get; } = [.. Enumerable.Range(1, Count).Select(Create)];

    public static NumberedDocument Numbered(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, Count);

        return All[number - 1];
    }

    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var document in All)
        {
            services.AddNumberedDocument(document);
        }
    }

    private static NumberedDocument Create(int number)
    {
        var suffix = number.ToString("00", CultureInfo.InvariantCulture);

        return number % 2 == 1
            ? new NumberedDocument($"test.numbering.invoices-{suffix}", $"Test invoices {suffix}", "Tax invoices the tests set numbering up for.", "46(b)")
            : new NumberedDocument($"test.numbering.notes-{suffix}", $"Test notes {suffix}", "Credit and debit notes the tests set numbering up for.", "53(1A)(c)");
    }
}
