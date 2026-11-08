using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Numbering;

public sealed class NumberedDocumentCatalogTests
{
    private static readonly NumberedDocument Invoices = new("test.numbering.invoices", "Test invoices", "Invoices the tests number.", "1(a)");

    private static readonly NumberedDocument Notes = new("test.numbering.credit-notes", "Test credit notes", "Credit notes the tests number.", "2(b)");

    [Fact]
    public void Catalog_OfDeclaredDocuments_ListsThemByTitleAndFindsEachByKey()
    {
        var catalog = new NumberedDocumentCatalog([new(Invoices), new(Notes)]);

        Assert.Equal([Notes, Invoices], catalog.Documents);
        Assert.Same(Invoices, catalog.Find("test.numbering.invoices"));
        Assert.Null(catalog.Find("test.numbering.receipts"));
    }

    [Fact]
    public void Catalog_ADocumentDeclaredTwice_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new NumberedDocumentCatalog([new(Invoices), new(Invoices with { Title = "Other" })]));
    }

    [Theory]
    [InlineData("Test.numbering.invoices")]
    [InlineData("test.numbering")]
    [InlineData("test..invoices")]
    [InlineData("test.numbering.invoices\n")]
    public void AddNumberedDocument_AKeyThatIsNotThreeKebabSegments_Throws(string key)
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddNumberedDocument(Invoices with { Key = key }));
    }

    [Theory]
    [InlineData("", "Invoices the tests number.", "1(a)")]
    [InlineData("Test invoices", " ", "1(a)")]
    [InlineData("Test invoices", "Invoices the tests number.", "")]
    public void AddNumberedDocument_ABlankTitleDescriptionOrRule_Throws(string title, string description, string rule)
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddNumberedDocument(new NumberedDocument("test.numbering.invoices", title, description, rule)));
    }

    [Fact]
    public void AddNumberedDocument_TheSameKeyTwice_Throws()
    {
        var services = new ServiceCollection().AddNumberedDocument(Invoices);

        Assert.Throws<InvalidOperationException>(() => services.AddNumberedDocument(Invoices));
    }

    [Fact]
    public void AddNumberedDocument_ADocument_JoinsTheCatalog()
    {
        using var provider = new ServiceCollection().AddNumberedDocument(Invoices).AddNumberedDocument(Notes).BuildServiceProvider();

        var catalog = new NumberedDocumentCatalog(provider.GetServices<NumberedDocumentRegistration>());

        Assert.Equal(2, catalog.Documents.Count);
    }
}
