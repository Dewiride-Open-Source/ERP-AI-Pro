using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dewiride.Erp.BuildingBlocks.Endpoints.OpenApi;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.OpenApi;

public sealed class SchemaReferenceIdsTests
{
    [Fact]
    public void Create_SameTypeTwice_ReturnsTheSameId()
    {
        var ids = new SchemaReferenceIds(_ => "Invoice");

        Assert.Equal("Invoice", ids.Create(Info<InvoiceResponse>()));
        Assert.Equal("Invoice", ids.Create(Info<InvoiceResponse>()));
    }

    [Fact]
    public void Create_ValueTypeAndItsNullableForm_ShareTheId()
    {
        var ids = new SchemaReferenceIds(_ => "Amount");

        Assert.Equal("Amount", ids.Create(Info<Amount>()));
        Assert.Equal("Amount", ids.Create(Info<Amount?>()));
    }

    [Fact]
    public void Create_TwoTypesMappingToOneId_ThrowsNamingBoth()
    {
        var ids = new SchemaReferenceIds(_ => "Invoice");
        ids.Create(Info<InvoiceResponse>());

        var error = Assert.Throws<InvalidOperationException>(() => ids.Create(Info<OtherInvoiceResponse>()));

        Assert.Contains(typeof(InvoiceResponse).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(OtherInvoiceResponse).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains("'Invoice'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_TypeTheInnerFactoryInlines_ReturnsNull()
    {
        var ids = new SchemaReferenceIds(_ => null);

        Assert.Null(ids.Create(Info<string>()));
        Assert.Null(ids.Create(Info<int>()));
    }

    private static JsonTypeInfo Info<T>() => JsonSerializerOptions.Default.GetTypeInfo(typeof(T));

    private sealed record InvoiceResponse(string Number);

    private sealed record OtherInvoiceResponse(string Number);

    private readonly record struct Amount(decimal Value);
}
