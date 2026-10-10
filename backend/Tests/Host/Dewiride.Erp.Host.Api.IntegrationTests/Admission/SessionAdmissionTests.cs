using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Admission;

public sealed class SessionAdmissionTests(SessionAdmissionTests.Fixture fixture) : IClassFixture<SessionAdmissionTests.Fixture>
{
    private const string SessionCookie = "__Host-erp-session";

    private const string ClearedCookie = "expires=Thu, 01 Jan 1970";

    private static readonly Uri MePath = new(AuthPaths.Me, UriKind.Relative);

    [Fact]
    public async Task SignIn_PersonWithoutARecord_CreatesTheirActiveRecordAsTheirOwn()
    {
        var person = PersonRecords.NewPerson(fixture.Factory);
        using var client = TestSignIn.CreateClient(fixture.Factory);

        using var signIn = await TestSignIn.SignInAsync(client, person);

        var record = await PersonRecords.OfAsync(fixture.Factory, person);
        Assert.NotNull(record);
        Assert.True(record.IsActive);
        Assert.Equal(person.Name, record.DisplayName);
        Assert.Equal(person.UserName, record.WorkEmail);
        Assert.NotNull(record.LastSignedInAt);
        Assert.Equal(person.ObjectId, record.CreatedBy);
    }

    [Fact]
    public async Task SignIn_PersonRegisteredBeforehandByTheirWorkEmail_SignsInToThatRecordAndLinksIt()
    {
        var person = PersonRecords.NewPerson(fixture.Factory);
        var registered = await PersonRecords.RegisterAsync(fixture.Factory, person);
        using var client = TestSignIn.CreateClient(fixture.Factory);

        using var signIn = await TestSignIn.SignInAsync(client, person);

        var record = await PersonRecords.OfAsync(fixture.Factory, person);
        Assert.NotNull(record);
        Assert.Equal(registered, record.Id.Value);
        Assert.Equal(person.ObjectId, record.EntraObjectId);
        Assert.NotNull(record.LastSignedInAt);
    }

    [Fact]
    public async Task Get_SessionOfAPersonDeactivatedSinceTheySignedIn_AnswersUnauthenticatedAndClearsTheSessionCookie()
    {
        var person = PersonRecords.NewPerson(fixture.Factory);
        using var client = await SignedInClientAsync(person);
        await PersonRecords.DeactivateAsync(fixture.Factory, person);

        using var response = await client.GetAsync(MePath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal) && cookie.Contains(ClearedCookie, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Get_SessionOfAPersonWhoseRecordWasDeletedSinceTheySignedIn_AnswersUnauthenticated()
    {
        var person = PersonRecords.NewPerson(fixture.Factory);
        using var client = await SignedInClientAsync(person);
        await PersonRecords.DeleteAsync(fixture.Factory, person);

        using var response = await client.GetAsync(MePath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_PersonReactivatedAfterADeactivation_IsSignedInAgain()
    {
        var person = PersonRecords.NewPerson(fixture.Factory);
        using (var first = await SignedInClientAsync(person))
        {
            await PersonRecords.DeactivateAsync(fixture.Factory, person);
            using var refused = await first.GetAsync(MePath, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        await PersonRecords.ReactivateAsync(fixture.Factory, person);

        using var client = await SignedInClientAsync(person);
        using var response = await client.GetAsync(MePath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpClient> SignedInClientAsync(TestUser person)
    {
        var client = TestSignIn.CreateClient(fixture.Factory);
        using (var signIn = await TestSignIn.SignInAsync(client, person))
        {
            using var signedIn = await client.GetAsync(MePath, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        }

        return client;
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(TestSignIn.Map);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
