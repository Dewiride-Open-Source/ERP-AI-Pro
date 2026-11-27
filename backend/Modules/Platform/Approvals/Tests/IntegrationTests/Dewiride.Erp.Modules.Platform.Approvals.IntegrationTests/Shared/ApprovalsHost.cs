using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Approvals;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Shared;

// A test host that declares the test kinds of approval, whose requests a test submits as a module's command would, here as the
// system. A person signs in through the real session cookie: the first sign-in links their record to their Microsoft Entra object
// id, by which a position names them as a manager and a request names them, and the roles an administrator assigns in the ERP count
// only for a session, because a request signed in by the test header runs no admission. Every test of the process writes into one
// database and a request waiting for a role waits for every holder of it, so a test that reads a box, a count or a setting takes an
// API kind of its own and new people for every part they play. A position starts long enough ago to name the manager of a request
// submitted the most working days ago a test seeds.
public sealed class ApprovalsHost : IAsyncDisposable
{
    private const string UsersRoute = "/api/identity/users";

    private const int PositionDaysBack = 60;

    public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestServices(TestApprovalKinds.Register).WithTestEndpoints(TestSignIn.Map);

    public HttpClient Administrator() => Factory.CreateClient().AsUser(TestUsers.Administrator);

    public HttpClient Employee() => Factory.CreateClient().AsUser(TestUsers.Accountant);

    public async Task<ApprovalPerson> SignInAsync(string name, bool administrator = false)
    {
        var objectId = Guid.CreateVersion7();
        var user = new TestUser(objectId, name, $"person.{objectId:N}@dewiride.test", administrator ? [AppRoles.User, AppRoles.Administrator] : [AppRoles.User]);
        Factory.Services.GetRequiredService<TestTokenEndpoint>().Admit(user);
        var client = TestSignIn.CreateClient(Factory);
        using var signIn = await TestSignIn.SignInAsync(client, user);
        using var profile = await client.GetAsync(new Uri($"{UsersRoute}/me", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var document = JsonDocument.Parse(await profile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return new ApprovalPerson(user, document.RootElement.GetProperty("id").GetGuid(), client);
    }

    public async Task ReportsToAsync(ApprovalPerson person, ApprovalPerson manager)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(manager);

        using var administrator = Administrator();
        var effectiveFrom = IndianStandardTime.Today(Factory.Services.GetRequiredService<TimeProvider>()).AddDays(-PositionDaysBack);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{UsersRoute}/{person.RecordId}/positions", UriKind.Relative))
        {
            Content = JsonContent.Create(new { effectiveFrom, managerId = manager.RecordId }),
        };
        request.Headers.Add(ApprovalsApi.IdempotencyKeyHeader, Guid.CreateVersion7().ToString());

        using var response = await administrator.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    public async Task AssignAsync(ApprovalPerson person, string role)
    {
        ArgumentNullException.ThrowIfNull(person);

        using var administrator = Administrator();
        using var read = await administrator.GetAsync(new Uri($"{UsersRoute}/{person.RecordId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var document = JsonDocument.Parse(await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var version = document.RootElement.GetProperty("version").GetString();

        using var assigned = await administrator.PutAsJsonAsync(new Uri($"{UsersRoute}/{person.RecordId}/roles", UriKind.Relative), new { roles = new[] { role }, version }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    public async Task<Guid> SubmitAsync(ApprovalKind kind, ApprovalPerson requestedFor, int submittedWorkingDaysAgo = 0, Guid? subjectId = null)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(requestedFor);

        var seed = new ApprovalRequestSeed(kind.Key, requestedFor.ObjectId)
        {
            SubmittedWorkingDaysAgo = submittedWorkingDaysAgo,
            SubjectId = subjectId ?? Guid.CreateVersion7(),
        };

        var submitted = await ApprovalRequestSeeder.SubmitAsync(Factory.Services, seed, TestContext.Current.CancellationToken);

        Assert.True(submitted.IsSuccess, submitted.Error?.Message);

        return Assert.NotNull(submitted.Value.RequestId).Value;
    }

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
