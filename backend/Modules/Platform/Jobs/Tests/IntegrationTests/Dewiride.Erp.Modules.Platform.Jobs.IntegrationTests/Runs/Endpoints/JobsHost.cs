using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints.TestJobs;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints;

// A request signed in by the test header runs no admission, so its persona may have no record to be named by; a test that reads
// the name of the administrator who acted signs in an administrator of its own, whose sign-in leaves that record. A test host
// reads no App Configuration, so the host is given the schedule of the account check that the store seeds.
public sealed class JobsHost : IAsyncDisposable
{
    public const string AccountCheckScheduleKey = "Erp:Identity:Users:AccountCheckSchedule";

    public const string AccountCheckSchedule = "30 0 * * *";

    private readonly ErpApiFactory _root;

    public JobsHost()
    {
        _root = new ErpApiFactory().WithConfiguration(AccountCheckScheduleKey, AccountCheckSchedule).WithTestEndpoints(TestSignIn.Map);
        Factory = _root.WithTestJobs();
    }

    public WebApplicationFactory<Program> Factory { get; }

    public TimeProvider Clock => Factory.Services.GetRequiredService<TimeProvider>();

    public HttpClient Administrator() => Factory.CreateClient().AsUser(TestUsers.Administrator);

    public TestUser AdmitAdministrator(string name)
    {
        var person = new TestUser(Guid.CreateVersion7(), name, $"administrator.{Guid.CreateVersion7():N}@example.com", [AppRoles.User, AppRoles.Administrator]);
        Factory.Services.GetRequiredService<TestTokenEndpoint>().Admit(person);

        return person;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _root.DisposeAsync();
    }
}
