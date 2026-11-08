using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Numbering;

namespace Dewiride.Erp.Modules.Platform.Numbering.IntegrationTests.Series.Endpoints;

// A test host that declares the test kinds of document; a test that changes a numbering takes a kind of its own.
public sealed class NumberingHost : IAsyncDisposable
{
    public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestServices(TestNumberedDocuments.Register);

    public HttpClient Administrator() => Factory.CreateClient().AsUser(TestUsers.Administrator);

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
