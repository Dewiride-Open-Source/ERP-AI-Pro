using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Shared;

// A person signed in through the session cookie, with the record their first sign-in left and the client that carries the session.
public sealed class ApprovalPerson(TestUser user, Guid recordId, HttpClient client) : IDisposable
{
    public Guid ObjectId => user.ObjectId;

    public string Name => user.Name;

    public Guid RecordId => recordId;

    public HttpClient Client => client;

    public void Dispose() => client.Dispose();
}
