using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Testing;
using HostModules = Dewiride.Erp.Host.Composition.Modules;

namespace Dewiride.Erp.ArchitectureTests.Rules;

// A job is no service, so a constructor that asks for a service the API does not compose would fail only when the runner builds
// the job for an attempt; the activation here is the runner's own, in a scope of the API host, so such a job fails the build.
public sealed class JobTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string OneSentence = @"\A[A-Z][^.!?\r\n]*\.\z";

    [Fact]
    public async Task Jobs_CanEveryOneBeActivatedInTheApiHost()
    {
        var jobs = Registrations();
        var failures = new List<string>();

        foreach (var registration in jobs)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            try
            {
                var job = ActivatorUtilities.CreateFactory(registration.JobType, Type.EmptyTypes)(scope.ServiceProvider, null);
                Assert.IsType(registration.JobType, job);
                if (job is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (job is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (InvalidOperationException exception)
            {
                failures.Add($"{registration.Name} ({registration.JobType.FullName}): {exception.Message}");
            }
        }

        Assert.NotEmpty(jobs);
        Assert.Empty(failures);
    }

    [Fact]
    public void JobNames_AreUniqueAndModuleJobsUseTheirModulesPrefix()
    {
        var jobs = Registrations();

        var outsideTheirModule = jobs
            .Where(registration => ModuleOf(registration.JobType) is not { } module || !registration.Name.StartsWith(module.Descriptor.PermissionPrefix, StringComparison.Ordinal))
            .Select(registration => $"{registration.Name} ({registration.JobType.FullName})")
            .ToList();

        Assert.NotEmpty(jobs);
        Assert.All(jobs, registration => Assert.True(JobNames.IsWellFormed(registration.Name), $"{registration.Name} is not three lower-case kebab segments."));
        Assert.Equal(jobs.Count, jobs.Select(registration => registration.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Empty(outsideTheirModule);
    }

    [Fact]
    public void JobDefinitions_DescribeEachJobInOneSentence()
    {
        var definitions = Registrations().Select(registration => registration.Definition).ToList();

        Assert.NotEmpty(definitions);
        Assert.All(definitions, definition => Assert.Matches(OneSentence, definition.Description));
    }

    private static IModule? ModuleOf(Type jobType) => HostModules.All.SingleOrDefault(module => module.GetType().Assembly == jobType.Assembly);

    private List<JobRegistration> Registrations() => [.. factory.Services.GetServices<JobRegistration>()];
}
