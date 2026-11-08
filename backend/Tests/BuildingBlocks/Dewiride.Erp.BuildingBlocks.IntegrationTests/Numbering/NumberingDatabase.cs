using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Numbering.Sample;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;
using Dewiride.Erp.BuildingBlocks.Numbering.Allocation;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Dewiride.Erp.BuildingBlocks.Numbering.Rules;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Numbering;

// The sample context, which maps the numbering tables as a module that numbers its documents does, over the test process's
// database, whose migrations created those tables, with the numbering services, one kind of document of the test's own, so that
// the tests of the process share the tables, and a made-up rule in place of the parameter store's, so that no statutory value is
// written here. Today is 8 November 2026 in India, in the financial year 2026-27.
internal sealed class NumberingDatabase : IAsyncDisposable
{
    public static readonly SerialNumberRule Rule = new(14, "-.");

    private readonly ServiceProvider _provider;

    private NumberingDatabase(ServiceProvider provider, NumberedDocument document, SwitchableRules rules)
    {
        _provider = provider;
        Document = document;
        Rules = rules;
    }

    public static FinancialYear CurrentYear { get; } = new(2026);

    public static DateOnly Today { get; } = new(2026, 11, 8);

    public NumberedDocument Document { get; }

    public SwitchableRules Rules { get; }

    public FakeTimeProvider Clock => _provider.GetRequiredService<FakeTimeProvider>();

    public TestActorContext Actor => _provider.GetRequiredService<TestActorContext>();

    public static async Task<NumberingDatabase> CreateAsync()
    {
        var document = new NumberedDocument(
            $"test.numbering.invoices-{Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[^12..]}",
            "Test invoices",
            "Invoices the numbering tests number.",
            "test-rule");
        var rules = new SwitchableRules();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 8, 6, 0, 0, TimeSpan.Zero));
        var actor = new TestActorContext { ActorId = Guid.CreateVersion7() };

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:ConnectionString"] = SqlTestDatabase.Current.ConnectionString,
                [ErpConfigurationSourceResolver.SourceSetting] = ErpConfigurationSourceResolver.InMemorySource,
            })
            .Build());
        services.AddLogging();
        services.AddSingleton(clock);
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton(actor);
        services.AddScoped<IActorContext>(provider => provider.GetRequiredService<TestActorContext>());
        services.AddScoped<IRequestOrigin, TestRequestOrigin>();
        services.AddErpPersistenceCore();
        services.AddModuleDbContext<SampleDbContext>(SampleDbContext.SchemaName);
        services.AddModuleDbContext<NumberingDbContext>(NumberingDbContext.SchemaName);
        services.AddModuleDbContext<AuditingDbContext>(AuditingDbContext.SchemaName);
        services.AddScoped<DomainEventDispatcher>();
        services.AddSingleton(new NumberedDocumentCatalog([new NumberedDocumentRegistration(document)]));
        services.AddSingleton(rules);
        services.AddScoped<ISerialNumberRules>(provider => provider.GetRequiredService<SwitchableRules>());
        services.AddScoped<IDocumentNumbers, DocumentNumbers>();
        services.AddScoped<INumberingAdministration, NumberingAdministration>();
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<DatabaseMigrator>().MigrateAllAsync(CancellationToken.None);

        return new NumberingDatabase(provider, document, rules);
    }

    public async Task<T> AdministerAsync<T>(Func<INumberingAdministration, CancellationToken, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = _provider.CreateAsyncScope();

        return await action(scope.ServiceProvider.GetRequiredService<INumberingAdministration>(), TestContext.Current.CancellationToken);
    }

    // Issues one invoice dated documentDate in a unit of work of its own, as an issuing command does, and rolls it back after
    // its save when rollBack is set; the answer is the number the invoice was given, or the refusal.
    public async Task<Result<DocumentNumber>> IssueAsync(DateOnly documentDate, bool rollBack = false, string? documentType = null)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var numbers = scope.ServiceProvider.GetRequiredService<IDocumentNumbers>();
        var cancellationToken = TestContext.Current.CancellationToken;

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync(token);
                var number = await numbers.NextAsync(context, documentType ?? Document.Key, documentDate, token);
                if (number.IsFailure)
                {
                    await transaction.RollbackAsync(token);
                    return number;
                }

                context.Invoices.Add(SampleInvoice.Issue(Document.Key, number.Value));
                await context.SaveChangesAsync(token);
                if (rollBack)
                {
                    await transaction.RollbackAsync(token);
                }
                else
                {
                    await transaction.CommitAsync(token);
                }

                return number;
            },
            cancellationToken);
    }

    public async Task RunWithoutTransactionAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        await scope.ServiceProvider.GetRequiredService<IDocumentNumbers>().NextAsync(context, Document.Key, Today, TestContext.Current.CancellationToken);
    }

    public async Task RunOnContextAsync<TContext>()
        where TContext : ModuleDbContext
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var numbers = scope.ServiceProvider.GetRequiredService<IDocumentNumbers>();

        await context.Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(token);
                await numbers.NextAsync(context, Document.Key, Today, token);
            },
            TestContext.Current.CancellationToken);
    }

    public async Task<Result<string>> RunUnitAsync(Func<SampleDbContext, IDocumentNumbers, CancellationToken, Task<Result<string>>> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var numbers = scope.ServiceProvider.GetRequiredService<IDocumentNumbers>();

        return await EfUnitOfWork.RunAsync(
            context,
            scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>(),
            scope.ServiceProvider.GetRequiredService<UnitOfWorkSignal>(),
            token => body(context, numbers, token),
            TestContext.Current.CancellationToken);
    }

    public async Task<IReadOnlyList<SampleInvoice>> InvoicesAsync()
    {
        await using var scope = _provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Invoices
            .AsNoTracking()
            .Where(invoice => invoice.DocumentType == Document.Key)
            .OrderBy(invoice => invoice.Year)
            .ThenBy(invoice => invoice.Sequence)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    public async Task<IReadOnlyList<ChangeRecord>> ChangeRecordsOfAsync(Guid entityId)
    {
        await using var scope = _provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AuditingDbContext>().ChangeRecords
            .AsNoTracking()
            .Where(record => record.EntityId == entityId)
            .OrderBy(record => record.OccurredAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    public async Task<T> QueryAsync<T>(Func<NumberingDbContext, CancellationToken, Task<T>> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var scope = _provider.CreateAsyncScope();

        return await query(scope.ServiceProvider.GetRequiredService<NumberingDbContext>(), TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    // Answers the made-up rule for every key and year, unless a test takes it away.
    internal sealed class SwitchableRules : ISerialNumberRules
    {
        public SerialNumberRule? Current { get; set; } = Rule;

        public Task<SerialNumberRule?> RuleForAsync(string rule, FinancialYear year, CancellationToken cancellationToken) => Task.FromResult(Current);
    }
}
