using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Definitions;

public sealed class JobCatalogTests
{
    private static readonly JobDefinition First = new("test.jobs.first", "First job", "Does the first thing.", JobRetryPolicy.Default, TimeSpan.FromMinutes(1));

    private static readonly JobDefinition Second = new("test.jobs.second", "Second job", "Does the second thing.", JobRetryPolicy.Default, TimeSpan.FromMinutes(1));

    [Fact]
    public void Jobs_Registered_AreListedByNameWithTheRecurringOnesApart()
    {
        using var provider = new ServiceCollection()
            .AddOptions()
            .AddJob<SecondJob, ItemPayload>(Second)
            .AddRecurringJob<FirstJob, ScheduleOptions>(First, options => options.Schedule)
            .BuildServiceProvider();

        var catalog = new JobCatalog(provider.GetServices<JobRegistration>());

        Assert.Equal(["test.jobs.first", "test.jobs.second"], catalog.Names);
        Assert.Equal(["test.jobs.first"], catalog.Recurring.Select(registration => registration.Name));
        Assert.Equal(typeof(ItemPayload), catalog.Find(typeof(SecondJob))!.PayloadType);
        Assert.Same(catalog.Find("test.jobs.first"), catalog.Find(typeof(FirstJob)));
    }

    [Fact]
    public void AddRecurringJob_Schedule_IsReadFromTheCurrentOptions()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Test:Schedule"] = "15 3 * * *" }).Build();
        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddRecurringJob<FirstJob, ScheduleOptions>(First, options => options.Schedule)
            .Configure<ScheduleOptions>(configuration.GetSection("Test"))
            .BuildServiceProvider();

        var registration = Assert.Single(provider.GetServices<JobRegistration>());

        Assert.Equal("15 3 * * *", registration.Schedule!(provider));
    }

    [Fact]
    public void AddJob_SameNameTwice_IsRefused()
    {
        var services = new ServiceCollection().AddJob<FirstJob>(First);

        var refusal = Assert.Throws<InvalidOperationException>(() => services.AddJob<OtherJob>(First));

        Assert.Contains("test.jobs.first", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddJob_SameTypeUnderTwoNames_IsRefused()
    {
        var services = new ServiceCollection().AddJob<FirstJob>(First);

        var refusal = Assert.Throws<InvalidOperationException>(() => services.AddJob<FirstJob>(Second));

        Assert.Contains(nameof(FirstJob), refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RegistrationsThatShareAName_AreRefused()
    {
        JobRegistration[] registrations =
        [
            new(First, typeof(FirstJob), null, static (_, _, _, _) => Task.FromResult(Result.Success()), null),
            new(First, typeof(OtherJob), null, static (_, _, _, _) => Task.FromResult(Result.Success()), null),
        ];

        Assert.Throws<InvalidOperationException>(() => new JobCatalog(registrations));
    }

    [Theory]
    [InlineData("Test.jobs.first", "Title", "Description.", 5, 60)]
    [InlineData("test.jobs.first", "", "Description.", 5, 60)]
    [InlineData("test.jobs.first", "Title", " ", 5, 60)]
    [InlineData("test.jobs.first", "Title", "Description.", 0, 60)]
    [InlineData("test.jobs.first", "Title", "Description.", 5, 0)]
    [InlineData("test.jobs.first", "Title", "Description.", 5, 86401)]
    public void AddJob_InvalidDefinition_IsRefused(string name, string title, string description, int maxAttempts, int timeoutSeconds)
    {
        var definition = new JobDefinition(name, title, description, JobRetryPolicy.Default with { MaxAttempts = maxAttempts }, TimeSpan.FromSeconds(timeoutSeconds));

        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddJob<FirstJob>(definition));
    }

    [Fact]
    public void Require_UnregisteredJob_IsRefused()
    {
        var catalog = new JobCatalog([]);

        var refusal = Assert.Throws<InvalidOperationException>(() => catalog.Require(typeof(FirstJob), null));

        Assert.Contains("AddJob", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Require_PayloadOfAnotherType_IsRefused()
    {
        using var provider = new ServiceCollection().AddJob<SecondJob, ItemPayload>(Second).BuildServiceProvider();
        var catalog = new JobCatalog(provider.GetServices<JobRegistration>());

        Assert.Throws<InvalidOperationException>(() => catalog.Require(typeof(SecondJob), null));
        Assert.Equal("test.jobs.second", catalog.Require(typeof(SecondJob), typeof(ItemPayload)).Name);
    }

    private sealed record ItemPayload(Guid ItemId);

    private sealed class ScheduleOptions
    {
        public string Schedule { get; set; } = string.Empty;
    }

    private sealed class FirstJob : IJob
    {
        public Task<Result> RunAsync(JobContext context, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    private sealed class OtherJob : IJob
    {
        public Task<Result> RunAsync(JobContext context, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    private sealed class SecondJob : IJob<ItemPayload>
    {
        public Task<Result> RunAsync(ItemPayload payload, JobContext context, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }
}
