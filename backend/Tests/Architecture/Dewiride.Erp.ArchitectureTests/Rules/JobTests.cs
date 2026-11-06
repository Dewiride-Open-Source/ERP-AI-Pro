using Dewiride.Erp.ArchitectureTests.Rules.Probes.Events;
using Dewiride.Erp.ArchitectureTests.Rules.Probes.Integration.Consumers;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Testing;
using HostModules = Dewiride.Erp.Host.Composition.Modules;

namespace Dewiride.Erp.ArchitectureTests.Rules;

// A job is no service, so a constructor that asks for a service the API does not compose would fail only when the runner builds
// the job for an attempt; the activation here is the runner's own, in a scope of the API host, so such a job fails the build. A
// delivery job builds its subscriber the same way when it runs, so the subscriber is built here as well.
public sealed class JobTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string OneSentence = @"\A[A-Z][^.!?\r\n]*\.\z";

    private static readonly Type ProbeDelivery = typeof(IntegrationEventDeliveryJob<ProbeEvents.LedgerPosted, ProbeConsumers.LedgerPostedRecorder>);

    [Fact]
    public async Task Jobs_CanEveryOneBeActivatedInTheApiHost()
    {
        var jobs = Registrations();

        var failures = await ActivationProblemsAsync(jobs.Select(registration => (registration.Name, registration.JobType)));

        Assert.NotEmpty(jobs);
        Assert.Empty(failures);
    }

    [Fact]
    public async Task ActivationProblems_ProbeDeliveryJobs_AcceptASubscriberTheHostCanBuildAndRefuseOneItCannot()
    {
        var unbuildable = typeof(IntegrationEventDeliveryJob<ProbeEvents.LedgerPosted, ProbeConsumers.LedgerPostedChecker>);

        Assert.Empty(await ActivationProblemsAsync([("probe.ledgers.ledger-posted-recorder", ProbeDelivery)]));
        Assert.Contains(typeof(ProbeConsumers.LedgerPostedChecker).FullName!, Assert.Single(await ActivationProblemsAsync([("probe.ledgers.ledger-posted-checker", unbuildable)])), StringComparison.Ordinal);
    }

    [Fact]
    public void JobNames_AreUniqueAndModuleJobsUseTheirModulesPrefix()
    {
        var jobs = Registrations();

        Assert.NotEmpty(jobs);
        Assert.All(jobs, registration => Assert.True(JobNames.IsWellFormed(registration.Name), $"{registration.Name} is not three lower-case kebab segments."));
        Assert.Equal(jobs.Count, jobs.Select(registration => registration.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Empty(OutsideTheirModule(jobs.Select(registration => (registration.Name, registration.JobType)), owner => ModuleOf(owner)?.Descriptor.PermissionPrefix));
    }

    [Fact]
    public void OutsideTheirModule_ProbeDeliveryJob_IsNamedUnderItsSubscribersPrefix()
    {
        static string? PrefixOf(Type owner) => owner == typeof(ProbeConsumers.LedgerPostedRecorder) ? "probe.ledgers." : null;

        Assert.Empty(OutsideTheirModule([("probe.ledgers.ledger-posted-recorder", ProbeDelivery)], PrefixOf));
        Assert.Single(OutsideTheirModule([("probe.other.ledger-posted-recorder", ProbeDelivery)], PrefixOf));
    }

    [Fact]
    public void JobDefinitions_DescribeEachJobInOneSentence()
    {
        var definitions = Registrations().Select(registration => registration.Definition).ToList();

        Assert.NotEmpty(definitions);
        Assert.All(definitions, definition => Assert.Matches(OneSentence, definition.Description));
    }

    // A delivery job is the building block's own class, closed over the subscriber that a module declares.
    internal static Type OwnerOf(Type jobType) =>
        jobType.IsGenericType && jobType.GetGenericTypeDefinition() == typeof(IntegrationEventDeliveryJob<,>) ? jobType.GetGenericArguments()[1] : jobType;

    private static List<string> OutsideTheirModule(IEnumerable<(string Name, Type JobType)> jobs, Func<Type, string?> prefixOf) =>
        [.. jobs
            .Where(job => prefixOf(OwnerOf(job.JobType)) is not { } prefix || !job.Name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(job => $"{job.Name} ({job.JobType.FullName})")];

    private static IModule? ModuleOf(Type ownerType) => HostModules.All.SingleOrDefault(module => module.GetType().Assembly == ownerType.Assembly);

    private static async Task DisposeAsync(object instance)
    {
        if (instance is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (instance is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task<List<string>> ActivationProblemsAsync(IEnumerable<(string Name, Type JobType)> jobs)
    {
        var problems = new List<string>();
        foreach (var (name, jobType) in jobs)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            foreach (var type in new[] { jobType, OwnerOf(jobType) }.Distinct())
            {
                try
                {
                    await DisposeAsync(ActivatorUtilities.CreateFactory(type, Type.EmptyTypes)(scope.ServiceProvider, null));
                }
                catch (InvalidOperationException exception)
                {
                    problems.Add($"{name} ({type.FullName}): {exception.Message}");
                }
            }
        }

        return problems;
    }

    private List<JobRegistration> Registrations() => [.. factory.Services.GetServices<JobRegistration>()];
}
