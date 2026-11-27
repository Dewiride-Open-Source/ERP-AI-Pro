using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Dewiride.Erp.BuildingBlocks.Approvals;
using Dewiride.Erp.BuildingBlocks.Approvals.Administration;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;
using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Integration;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals;

// The persistence sample's context, which maps the approval tables as a module that asks for approval does, over the test process's
// database, with the approval services AddErpApprovals composes and kinds of the fixture's own, so the tests that run beside one
// another never meet in a list, a count or a setting; the people are new to each test, and their reporting lines and names are the
// test's. The sample's commands run through the application pipeline, so a decision moves the claim through a real command whose unit
// of work joins the decision's transaction. Each call runs in a scope of its own, as the person who makes it and with a correlation
// id of its own. The provider checks scopes but not every registration as it is built, because the pipeline registers every handler
// of this assembly, the other samples' among them, whose services this host does not compose. Today is Friday 27 November 2026,
// 10:00 in India.
internal sealed class ApprovalsDatabase : IAsyncDisposable
{
    public const int KindWorkingDays = 2;

    public static readonly DateTimeOffset Today = new(2026, 11, 27, 4, 30, 0, TimeSpan.Zero);

    private static readonly SemaphoreSlim MigrationGate = new(1, 1);

    private static bool _migrated;

    private readonly IHost _host;

    private ApprovalsDatabase(IHost host, ApprovalKind manager, ApprovalKind chain, ApprovalKind finance, ApprovalKind unmapped)
    {
        _host = host;
        ManagerKind = manager;
        ChainKind = chain;
        FinanceKind = finance;
        UnmappedKind = unmapped;
    }

    public ApprovalKind ManagerKind { get; }

    public ApprovalKind ChainKind { get; }

    public ApprovalKind FinanceKind { get; }

    public ApprovalKind UnmappedKind { get; }

    public FakeTimeProvider Clock => _host.Services.GetRequiredService<FakeTimeProvider>();

    public ScriptedReportingManagers Managers => _host.Services.GetRequiredService<ScriptedReportingManagers>();

    public NamedPeople People => _host.Services.GetRequiredService<NamedPeople>();

    public SampleClaimScript Script => _host.Services.GetRequiredService<SampleClaimScript>();

    public LockRequestWatch LockRequests => _host.Services.GetRequiredService<LockRequestWatch>();

    public FakeLogCollector Logs => _host.Services.GetRequiredService<FakeLogCollector>();

    public static Guid NewPerson() => Guid.CreateVersion7();

    public static ApprovalCaller Caller(Guid person, params ErpRole[] roles) => new(person, new HashSet<ErpRole>([ErpRole.Employee, .. roles]), SeesAll: false, DecidesAny: false);

    public static ApprovalCaller Administrator(Guid person) => new(person, new HashSet<ErpRole>([ErpRole.Employee, ErpRole.Administrator]), SeesAll: true, DecidesAny: true);

    public static async Task<ApprovalsDatabase> CreateAsync()
    {
        var suffix = Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[^12..];
        var manager = Kind($"manager-{suffix}", "Claim approval", ApprovalStepDefinition.Manager("Manager"));
        var chain = Kind($"chain-{suffix}", "Claim approval by manager and Finance", ApprovalStepDefinition.Manager("Manager"), ApprovalStepDefinition.HeldBy("Finance", ErpRole.Finance));
        var finance = Kind($"finance-{suffix}", "Claim approval by Finance", ApprovalStepDefinition.HeldBy("Finance", ErpRole.Finance));
        var unmapped = Kind($"unmapped-{suffix}", "Claim approval saved nowhere", ApprovalStepDefinition.Manager("Manager"));
        var database = new ApprovalsDatabase(Build([manager, chain, finance], unmapped), manager, chain, finance, unmapped);
        await MigrateOnceAsync(database._host);

        return database;
    }

    public async Task<(SampleClaimId Claim, Result<ApprovalSubmitted> Submitted)> SubmitClaimAsync(ApprovalKind kind, Guid requestedFor, Guid? submittedBy = null, bool receiptAttached = true, string title = "Taxi to the client's office")
    {
        ArgumentNullException.ThrowIfNull(kind);

        var claim = SampleClaimId.Create();
        var submitted = await AsAsync(
            submittedBy ?? requestedFor,
            (services, token) => services.GetRequiredService<ICommandHandler<SubmitSampleClaimCommand, ApprovalSubmitted>>()
                .HandleAsync(new SubmitSampleClaimCommand(claim, kind.Key, title, requestedFor, receiptAttached), token));

        return (claim, submitted);
    }

    public async Task<(SampleClaimId Claim, ApprovalRequestId Request)> SubmittedClaimAsync(ApprovalKind kind, Guid requestedFor, Guid? submittedBy = null, bool receiptAttached = true)
    {
        var (claim, submitted) = await SubmitClaimAsync(kind, requestedFor, submittedBy, receiptAttached);

        return (claim, submitted.Value.RequestId ?? throw new InvalidOperationException("The claim's kind needs no approval, so no request was submitted."));
    }

    public Task<Result<ApprovalSubmitted>> ResubmitClaimAsync(ApprovalKind kind, SampleClaimId claim, Guid actor)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return AsAsync(actor, (services, token) => services.GetRequiredService<ICommandHandler<ResubmitSampleClaimCommand, ApprovalSubmitted>>().HandleAsync(new ResubmitSampleClaimCommand(claim, kind.Key), token));
    }

    public Task<Result<SampleClaimState>> CancelClaimAsync(ApprovalKind kind, SampleClaimId claim, Guid actor)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return AsAsync(actor, (services, token) => services.GetRequiredService<ICommandHandler<CancelSampleClaimCommand, SampleClaimState>>().HandleAsync(new CancelSampleClaimCommand(claim, kind.Key), token));
    }

    public Task<Result<ApprovalRequestDetails>> DecideAsync(ApprovalRequestId id, ApprovalDecision decision, ApprovalCaller caller, string? comment = null, byte[]? version = null)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return AsAsync(caller.ObjectId, async (services, token) =>
            await services.GetRequiredService<IApprovalDecisions>().DecideAsync(id, decision, comment, version ?? await VersionOfAsync(id), caller, token));
    }

    public Task<Result<ApprovalRequestDetails>> WithdrawAsync(ApprovalRequestId id, ApprovalCaller caller, string? reason = null, byte[]? version = null)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return AsAsync(caller.ObjectId, async (services, token) =>
            await services.GetRequiredService<IApprovalDecisions>().WithdrawAsync(id, reason, version ?? await VersionOfAsync(id), caller, token));
    }

    public Task<T> InboxAsync<T>(ApprovalCaller caller, Func<IApprovalInbox, CancellationToken, Task<T>> read)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(read);

        return AsAsync(caller.ObjectId, (services, token) => read(services.GetRequiredService<IApprovalInbox>(), token));
    }

    public Task<T> AdministerAsync<T>(Guid actor, Func<IApprovalAdministration, CancellationToken, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return AsAsync(actor, (services, token) => action(services.GetRequiredService<IApprovalAdministration>(), token));
    }

    public Task<Result<T>> RunUnitAsync<T>(Guid actor, Func<SampleDbContext, IApprovals, CancellationToken, Task<Result<T>>> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return AsAsync(actor, (services, token) =>
        {
            var context = services.GetRequiredService<SampleDbContext>();
            var approvals = services.GetRequiredService<IApprovals>();

            return EfUnitOfWork.RunAsync(context, services.GetRequiredService<DomainEventDispatcher>(), services.GetRequiredService<UnitOfWorkSignal>(), unitToken => body(context, approvals, unitToken), token);
        });
    }

    public async Task<T> AsAsync<T>(Guid actor, Func<IServiceProvider, CancellationToken, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = _host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestActorContext>().ActorId = actor;

        return await action(scope.ServiceProvider, TestContext.Current.CancellationToken);
    }

    public async Task<T> WhileLockedAsync<T>(ApprovalRequestId id, Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = _host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(token);
                Assert.True(await ApprovalLock.TryAcquireAsync(context, id, token));
                var result = await action();
                await transaction.RollbackAsync(token);

                return result;
            },
            TestContext.Current.CancellationToken);
    }

    public async Task<byte[]> VersionOfAsync(ApprovalRequestId id) => (await RequestAsync(id)).RowVersion;

    public Task<ApprovalRequest> RequestAsync(ApprovalRequestId id) =>
        QueryAsync((context, token) => context.ApprovalRequests.AsNoTracking().Include(r => r.Steps).SingleAsync(r => r.Id == id, token));

    public Task<List<ApprovalRequest>> RequestsOfAsync(Guid subjectId) =>
        QueryAsync((context, token) => context.ApprovalRequests.AsNoTracking().Include(r => r.Steps).Where(r => r.SubjectId == subjectId).OrderBy(r => r.SubmittedAt).ToListAsync(token));

    public Task<SampleClaim?> FindClaimAsync(SampleClaimId id) =>
        QueryAsync((context, token) => context.Claims.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, token));

    public async Task<SampleClaim> ClaimAsync(SampleClaimId id) => await FindClaimAsync(id) ?? throw new InvalidOperationException($"The claim {id.Value} is not saved.");

    public async Task<T> QueryAsync<T>(Func<SampleDbContext, CancellationToken, Task<T>> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var scope = _host.Services.CreateAsyncScope();

        return await query(scope.ServiceProvider.GetRequiredService<SampleDbContext>(), TestContext.Current.CancellationToken);
    }

    public async Task<IReadOnlyList<ChangeRecord>> ChangeRecordsOfAsync(Guid entityId)
    {
        await using var scope = _host.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AuditingDbContext>().ChangeRecords
            .AsNoTracking()
            .Where(record => record.EntityId == entityId)
            .OrderBy(record => record.OccurredAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
        else
        {
            _host.Dispose();
        }
    }

    private static ApprovalKind Kind(string name, string title, params ApprovalStepDefinition[] steps) =>
        new($"test.approvals.{name}", title, "A sample claim waits for its approval before it is paid.", SampleClaimConfiguration.ChangeKey, steps, KindWorkingDays);

    private static IHost Build(IReadOnlyList<ApprovalKind> sampleKinds, ApprovalKind unmapped)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateScopes = true }));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [ErpConfigurationSourceResolver.SourceSetting] = ErpConfigurationSourceResolver.InMemorySource,
            [$"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ConnectionString)}"] = SqlTestDatabase.Current.ConnectionString,
        });
        var clock = new FakeTimeProvider(Today);
        var lockRequests = new LockRequestWatch();
        builder.Services.AddLogging();
        builder.Services.AddFakeLogging();
        builder.Services.AddSingleton(clock);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddScoped<TestActorContext>();
        builder.Services.AddScoped<IActorContext>(provider => provider.GetRequiredService<TestActorContext>());
        builder.Services.AddScoped<TestRequestOrigin>();
        builder.Services.AddScoped<IRequestOrigin>(provider => provider.GetRequiredService<TestRequestOrigin>());
        builder.Services.AddSingleton<ScriptedReportingManagers>();
        builder.Services.AddSingleton<IReportingManagers>(provider => provider.GetRequiredService<ScriptedReportingManagers>());
        builder.Services.AddSingleton<NamedPeople>();
        builder.Services.AddSingleton<IPersonNames>(provider => provider.GetRequiredService<NamedPeople>());
        builder.Services.AddSingleton<SampleClaimScript>();
        builder.Services.AddSingleton(lockRequests);
        builder.Services.AddErpPersistenceCore();
        builder.Services.AddModuleDbContext<SampleDbContext>(SampleDbContext.SchemaName);
        builder.Services.ConfigureDbContext<SampleDbContext>(options => options.AddInterceptors(lockRequests));
        builder.Services.AddModuleDbContext<AuditingDbContext>(AuditingDbContext.SchemaName);
        builder.Services.AddHandlersFromAssembly(typeof(ApprovalsDatabase).Assembly);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecordNames, SampleClaimNames>());
        builder.AddErpApprovals();
        foreach (var kind in sampleKinds)
        {
            builder.Services.AddApprovalKind<SampleDbContext, SampleClaimOutcomes>(kind);
        }

        builder.Services.AddApprovalKind<AuditingDbContext, SampleClaimOutcomes>(unmapped);

        return builder.Build();
    }

    private static async Task MigrateOnceAsync(IHost host)
    {
        await MigrationGate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (!_migrated)
            {
                await host.Services.GetRequiredService<DatabaseMigrator>().MigrateAllAsync(TestContext.Current.CancellationToken);
                _migrated = true;
            }
        }
        finally
        {
            MigrationGate.Release();
        }
    }
}
