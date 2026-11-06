using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Catalog;

public sealed class DbContextCatalogTests
{
    [Fact]
    public void Constructor_DistinctRegistrations_KeepsRegistrationOrder()
    {
        var first = new DbContextRegistration(typeof(string), "finance_sales");
        var second = new DbContextRegistration(typeof(int), "finance_payroll");

        var catalog = new DbContextCatalog([first, second]);

        Assert.Equal([first, second], catalog.Registrations);
    }

    [Fact]
    public void Constructor_SameContextTwice_ThrowsNamingTheContext()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DbContextCatalog([new DbContextRegistration(typeof(string), "finance_sales"), new DbContextRegistration(typeof(string), "finance_payroll")]));

        Assert.Contains("String", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_SameSchemaTwice_ThrowsNamingTheSchemaAndBothContexts()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DbContextCatalog([new DbContextRegistration(typeof(string), "finance_sales"), new DbContextRegistration(typeof(int), "Finance_Sales")]));

        Assert.Contains("finance_sales", exception.Message, StringComparison.Ordinal);
        Assert.Contains("String", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Int32", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnedBy_AssemblyOwningOneContext_AnswersItsRegistration()
    {
        var owned = new DbContextRegistration(typeof(DbContextCatalogTests), "finance_sales");
        var catalog = new DbContextCatalog([owned, new DbContextRegistration(typeof(string), "finance_payroll")]);

        Assert.Equal(owned, catalog.OwnedBy(typeof(DbContextCatalogTests).Assembly));
    }

    [Fact]
    public void OwnedBy_AssemblyOwningNoContext_AnswersNothing()
    {
        var catalog = new DbContextCatalog([new DbContextRegistration(typeof(string), "finance_payroll")]);

        Assert.Null(catalog.OwnedBy(typeof(DbContextCatalogTests).Assembly));
    }

    [Fact]
    public void OwnedBy_AssemblyOwningTwoContexts_ThrowsNamingBoth()
    {
        var catalog = new DbContextCatalog([new DbContextRegistration(typeof(string), "finance_sales"), new DbContextRegistration(typeof(int), "finance_payroll")]);

        var exception = Assert.Throws<InvalidOperationException>(() => catalog.OwnedBy(typeof(string).Assembly));

        Assert.Contains("String and Int32", exception.Message, StringComparison.Ordinal);
    }
}
