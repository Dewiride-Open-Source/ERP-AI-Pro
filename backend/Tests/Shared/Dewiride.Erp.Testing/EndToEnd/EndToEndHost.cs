using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;
using Dewiride.Erp.Testing.Blob;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Testing.EndToEnd;

// The API Playwright starts: the API a host test builds, with the test identity provider, the test header scheme, the
// persona sign-in, the routes that add people to the test directory and switch its refusal of the application's token, and the
// route that seeds job runs, listening on a port, on a database and a blob container of its own, so a run reaches neither Azure nor
// Microsoft Entra ID and leaves the developer's data alone. Playwright stops its servers on Windows by killing them, which
// leaves their database and container behind, so every start removes those whose host has gone, the session lock on the
// database and the lease on the container having ended with its process, once they are older than LeftoverAge. Every browser
// project's requests and every call of the web servers reach the API from loopback, one anonymous rate limit partition, so
// the host allows anonymous callers ten times the default. The host reads no App Configuration, so it is given the schedule of
// the account check that the store seeds, for the jobs page to show; like every test host it runs no job loop, so the check
// never runs on its own.
public static class EndToEndHost
{
    public const string EnvironmentName = "Testing";

    public const string DatabaseNamePrefix = "ErpAiProE2E_";

    public const string ContainerNamePrefix = "erpe2e-";

    public const string AnonymousPermitLimit = "6000";

    public const string AccountCheckScheduleKey = "Erp:Identity:Users:AccountCheckSchedule";

    public const string AccountCheckSchedule = "30 0 * * *";

    public static TimeSpan LeftoverAge { get; } = TimeSpan.FromMinutes(10);

    public static async Task RunAsync(string[] args, Func<string, string?> environmentVariable)
    {
        var settings = EndToEndHostSettings.Read(args, environmentVariable);

        // WebApplicationFactory reads the API's content root from MvcTestingAppManifest.json in the current directory, which a
        // test runner sets to the output directory and dotnet run leaves wherever it was started.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        await using var database = SqlTestDatabase.WithNamePrefix(DatabaseNamePrefix, LeftoverAge);
        await database.InitializeAsync();
        await using var container = BlobTestContainer.WithNamePrefix(ContainerNamePrefix, LeftoverAge);
        await container.InitializeAsync();
        await using var factory = ErpApiFactory.ForEnvironment(EnvironmentName)
            .WithConfiguration(ErpApiFactory.IdentityWebOriginKey, settings.WebOrigin)
            .WithConfiguration($"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.AnonymousPermitLimit)}", AnonymousPermitLimit)
            .WithConfiguration(AccountCheckScheduleKey, AccountCheckSchedule)
            .WithTestEndpoints(PersonaSignIn.Map)
            .WithTestEndpoints(DirectoryPeople.Map)
            .WithTestEndpoints(DirectoryApplicationAccess.Map)
            .WithTestEndpoints(JobRuns.Map);
        factory.UseKestrel(settings.Port);
        factory.StartServer();

        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = factory.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => stopping.TrySetResult());
        await stopping.Task;
    }
}
