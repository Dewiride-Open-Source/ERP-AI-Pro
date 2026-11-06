using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dewiride.Erp.ArchitectureTests.Rules.Probes.Events;
using Dewiride.Erp.ArchitectureTests.Rules.Probes.Integration.Consumers;
using Dewiride.Erp.ArchitectureTests.Rules.Probes.Integration.Publishers;
using Dewiride.Erp.ArchitectureTests.Rules.Probes.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.Kernel.Events;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using HostModules = Dewiride.Erp.Host.Composition.Modules;

namespace Dewiride.Erp.ArchitectureTests.Rules;

// No product module publishes or subscribes yet, so every rule runs twice: over the product's types, where it must find nothing,
// and over the probes of Rules/Probes and of this class, where it must accept the probe that keeps it and refuse each that breaks
// it. An event's members are those its serializer writes, read from the serializer's own contract, so a member the serializer
// includes without being a public property is checked too.
public sealed partial class IntegrationEventTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string ProbePrefix = "probe.ledgers.";

    private static readonly Type[] PlainValueTypes = [typeof(Guid), typeof(bool), typeof(int), typeof(DateOnly), typeof(DateTimeOffset)];

    private static readonly string[] ProcessedKey = [nameof(ProcessedMessage.EventId), nameof(ProcessedMessage.Subscriber)];

    private static readonly string[] LooseMembers = ["Amount", "Kind", "Note", "Tags", "Total"];

    [Fact]
    public void Events_AreSealedRecordsOfContractsNamedUniquelyUnderTheirModuleWithPlainMembers()
    {
        var events = ProductTypes().Where(IsEvent).ToList();

        Assert.Empty(EventProblems(events, PermissionPrefixOfContracts, IsInContractsAssembly));
        Assert.Empty(SharedNames(events));
    }

    [Fact]
    public void EventProblems_ProbeEvents_AcceptThePlainEventAndRefuseEachBrokenRule()
    {
        Assert.Empty(EventProblems([typeof(ProbeEvents.LedgerPosted)], _ => ProbePrefix, _ => true));
        Assert.Contains(EventProblems([typeof(ProbeEvents.LedgerOpened)], _ => ProbePrefix, _ => true), problem => problem.Contains("not a public sealed record", StringComparison.Ordinal));
        Assert.Contains(EventProblems([typeof(ProbeEvents.LedgerArchived)], _ => ProbePrefix, _ => true), problem => problem.Contains("not a public sealed record", StringComparison.Ordinal));
        Assert.Contains(EventProblems([typeof(ProbeEvents.LedgerHidden)], _ => ProbePrefix, _ => true), problem => problem.Contains("not a public sealed record", StringComparison.Ordinal));
        Assert.Contains(EventProblems([typeof(ProbeEvents.LedgerClosedElsewhere)], _ => ProbePrefix, _ => true), problem => problem.Contains("outside its module's prefix", StringComparison.Ordinal));
        Assert.Contains(EventProblems([typeof(ProbeEvents.LedgerPostedV2)], _ => ProbePrefix, _ => true), problem => problem.Contains("a version instead of a meaning", StringComparison.Ordinal));
        Assert.Contains(EventProblems([typeof(ProbeEvents.LedgerMisnamed)], _ => ProbePrefix, _ => true), problem => problem.Contains("not three lower-case kebab segments", StringComparison.Ordinal));
        Assert.Contains(EventProblems([typeof(MisplacedEvent)], _ => ProbePrefix, _ => true), problem => problem.Contains("not in an Events namespace", StringComparison.Ordinal));
        Assert.Contains(EventProblems([typeof(ProbeEvents.LedgerPosted)], _ => ProbePrefix, _ => false), problem => problem.Contains("not declared in a module's Contracts", StringComparison.Ordinal));
        Assert.Equal(LooseMembers, EventProblems([typeof(ProbeEvents.LedgerNoted)], _ => ProbePrefix, _ => true).Select(MemberOf).Order(StringComparer.Ordinal));
        Assert.Equal(["Comment"], EventProblems([typeof(ProbeEvents.LedgerCommented)], _ => ProbePrefix, _ => true).Select(MemberOf));
        Assert.Contains(EventProblems([typeof(ProbeEvents.LedgerRelabelled)], _ => ProbePrefix, _ => true), problem => problem.Contains("LedgerId names a converter of its own", StringComparison.Ordinal));
        Assert.Contains(EventProblems([typeof(ProbeEvents.LedgerRewritten)], _ => ProbePrefix, _ => true), problem => problem.Contains("LedgerRewritten names a converter of its own", StringComparison.Ordinal));
        Assert.Contains(EventProblems([typeof(ProbeEvents.LedgerExtended)], _ => ProbePrefix, _ => true), problem => problem.Contains("Rest keeps members its type does not declare", StringComparison.Ordinal));
        Assert.False(IsInContractsAssembly(typeof(ProbeEvents.LedgerPosted)));
    }

    [Fact]
    public void SharedNames_TwoEventsOfOneName_AreReported()
    {
        Assert.Equal(["probe.ledgers.ledger-posted"], SharedNames([typeof(ProbeEvents.LedgerPosted), typeof(ProbeEvents.LedgerPostedAgain), typeof(ProbeEvents.LedgerOpened)]));
        Assert.Empty(SharedNames([typeof(ProbeEvents.LedgerPosted), typeof(ProbeEvents.LedgerOpened)]));
    }

    [Fact]
    public void Subscribers_AreSealedClassesOfIntegrationConsumers()
    {
        Assert.Empty(SubscriberProblems(ProductTypes().Where(IsSubscriber)));
    }

    [Fact]
    public void SubscriberProblems_ProbeSubscribers_AcceptTheSealedConsumerAndRefuseTheOthers()
    {
        Assert.True(IsSubscriber(typeof(ProbeConsumers.LedgerPostedRecorder)));
        Assert.Empty(SubscriberProblems([typeof(ProbeConsumers.LedgerPostedRecorder)]));
        Assert.Single(SubscriberProblems([typeof(ProbeConsumers.LedgerPostedAuditor)]));
        Assert.Single(SubscriberProblems([typeof(MisplacedSubscriber)]));
    }

    [Fact]
    public void Publishers_AreClassesOfIntegrationPublishers()
    {
        Assert.Empty(PublisherProblems(ProductTypes().Where(IsPublisher)));
    }

    [Fact]
    public void PublisherProblems_ProbePublishers_AcceptThePublisherOfIntegrationPublishersAndRefuseAnyOther()
    {
        Assert.True(IsPublisher(typeof(ProbePublishers.LedgerPostedPublisher)));
        Assert.Empty(PublisherProblems([typeof(ProbePublishers.LedgerPostedPublisher)]));
        Assert.Single(PublisherProblems([typeof(MisplacedPublisher)]));
    }

    [Fact]
    public void Subscriptions_OfTheApiHost_RecordWhatTheyHandleInTheirModulesContext()
    {
        using var scope = factory.Services.CreateScope();
        var catalog = factory.Services.GetRequiredService<DbContextCatalog>();
        var handlers = factory.Services.GetRequiredService<IntegrationEventCatalog>().Subscriptions.Select(subscription => subscription.HandlerType);

        Assert.Empty(UnrecordedSubscriptions(handlers, assembly => catalog.OwnedBy(assembly) is { } owned ? ((DbContext)scope.ServiceProvider.GetRequiredService(owned.ContextType)).Model : null));
    }

    [Fact]
    public void UnrecordedSubscriptions_ProbeContexts_AcceptAContextWithProcessedRecordsAndRefuseAnyOther()
    {
        using var recording = new ProbeContexts.Recording();
        using var outboxOnly = new ProbeContexts.OutboxOnly();

        Assert.Empty(UnrecordedSubscriptions([typeof(ProbeConsumers.LedgerPostedRecorder)], _ => recording.Model));
        Assert.Single(UnrecordedSubscriptions([typeof(ProbeConsumers.LedgerPostedRecorder)], _ => outboxOnly.Model));
        Assert.Single(UnrecordedSubscriptions([typeof(ProbeConsumers.LedgerPostedRecorder)], _ => null));
    }

    [Fact]
    public void Tables_OfEveryCatalogueContext_AreMappedAsTheirConfigurationsSay()
    {
        using var scope = factory.Services.CreateScope();
        var problems = factory.Services.GetRequiredService<DbContextCatalog>().Registrations
            .SelectMany(registration => TableProblems(((DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType)).Model, registration.Schema))
            .ToList();

        Assert.Empty(problems);
    }

    [Fact]
    public void TableProblems_ProbeContexts_AcceptTheConfiguredTablesAndRefuseRemappedOnes()
    {
        using var recording = new ProbeContexts.Recording();
        using var remapped = new ProbeContexts.Remapped();
        using var rekeyed = new ProbeContexts.Rekeyed();

        Assert.Empty(TableProblems(recording.Model, ProbeContexts.Recording.SchemaName));
        Assert.Equal(3, TableProblems(remapped.Model, ProbeContexts.Remapped.SchemaName).Count);
        var rekeyedProblems = TableProblems(rekeyed.Model, ProbeContexts.Rekeyed.SchemaName);
        Assert.Equal(2, rekeyedProblems.Count);
        Assert.Contains(rekeyedProblems, problem => problem.Contains("filtered indexes", StringComparison.Ordinal));
        Assert.Contains(rekeyedProblems, problem => problem.Contains("keyed by event and subscriber", StringComparison.Ordinal));
    }

    [Fact]
    public void OwnerOf_DeliveryJob_IsItsSubscriberAndAnyOtherJobItself()
    {
        Assert.Equal(typeof(ProbeConsumers.LedgerPostedRecorder), JobTests.OwnerOf(typeof(IntegrationEventDeliveryJob<ProbeEvents.LedgerPosted, ProbeConsumers.LedgerPostedRecorder>)));
        Assert.Equal(typeof(MisplacedPublisher), JobTests.OwnerOf(typeof(MisplacedPublisher)));
    }

    private static IEnumerable<Type> ProductTypes() => ErpAssemblies.All.SelectMany(assembly => assembly.GetTypes());

    private static bool IsEvent(Type type) => type is { IsInterface: false, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(type);

    private static bool IsSubscriber(Type type) =>
        type is { IsClass: true, IsAbstract: false } && type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>));

    private static bool IsPublisher(Type type) =>
        type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(IIntegrationEventPublisher)));

    private static bool IsInContractsAssembly(Type type) => type.Assembly.GetName().Name!.EndsWith(ErpAssemblies.ContractsSuffix, StringComparison.Ordinal);

    private static string? PermissionPrefixOfContracts(Type type) =>
        HostModules.All.SingleOrDefault(module => type.Assembly.GetName().Name == module.GetType().Assembly.GetName().Name + ErpAssemblies.ContractsSuffix)?.Descriptor.PermissionPrefix;

    private static string? NameOf(Type eventType) => eventType.GetProperty(nameof(IIntegrationEvent.Name), BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;

    private static string MemberOf(string problem) => problem[(problem.LastIndexOf('.', problem.IndexOf(" is a ", StringComparison.Ordinal)) + 1)..problem.IndexOf(" is a ", StringComparison.Ordinal)];

    private static List<string> EventProblems(IEnumerable<Type> events, Func<Type, string?> prefixOf, Func<Type, bool> inContractsAssembly)
    {
        var problems = new List<string>();
        foreach (var type in events)
        {
            if (!type.IsVisible || !type.IsSealed || type.GetMethod("<Clone>$") is null)
            {
                problems.Add($"{type.FullName} is not a public sealed record");
            }

            if (!inContractsAssembly(type))
            {
                problems.Add($"{type.FullName} is not declared in a module's Contracts");
            }

            if (!(type.Namespace?.EndsWith(".Events", StringComparison.Ordinal) ?? false))
            {
                problems.Add($"{type.FullName} is not in an Events namespace");
            }

            var name = NameOf(type);
            if (!IntegrationEventNames.IsWellFormed(name))
            {
                problems.Add($"{type.FullName} is named '{name}', which is not three lower-case kebab segments");
            }
            else if (prefixOf(type) is not { } prefix || !name!.StartsWith(prefix, StringComparison.Ordinal))
            {
                problems.Add($"{type.FullName} is named '{name}', outside its module's prefix");
            }

            if (name is not null && VersionSuffix().IsMatch(name))
            {
                problems.Add($"{type.FullName} is named '{name}', a version instead of a meaning");
            }

            problems.AddRange(MemberProblems(type));
        }

        return problems;
    }

    private static IEnumerable<string> MemberProblems(Type type)
    {
        if (type.GetCustomAttribute<JsonConverterAttribute>() is not null)
        {
            yield return $"{type.FullName} names a converter of its own, which can write anything";
            yield break;
        }

        foreach (var property in IntegrationEventSerializer.Options.GetTypeInfo(type).Properties)
        {
            var member = (property.AttributeProvider as MemberInfo)?.Name ?? property.Name;
            if (property.IsExtensionData)
            {
                yield return $"{type.FullName}.{member} keeps members its type does not declare, which an event does not carry";
            }
            else if (property.CustomConverter is not null)
            {
                yield return $"{type.FullName}.{member} names a converter of its own, which can write anything";
            }
            else if (!IsPlainMember(property.PropertyType))
            {
                yield return $"{type.FullName}.{member} is a {property.PropertyType.Name}, which an event does not carry";
            }
        }
    }

    private static bool IsPlainMember(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) ? IsPlainValue(type.GetGenericArguments()[0]) : IsPlainValue(type);

    private static bool IsPlainValue(Type type)
    {
        var value = Nullable.GetUnderlyingType(type) ?? type;

        return PlainValueTypes.Contains(value) || (value.IsEnum && (IsInContractsAssembly(value) || value.Assembly == typeof(IIntegrationEvent).Assembly));
    }

    private static List<string> SharedNames(IEnumerable<Type> events) =>
        [.. events.GroupBy(NameOf, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key!)];

    private static List<string> SubscriberProblems(IEnumerable<Type> subscribers) =>
        [.. subscribers
            .Where(type => !type.IsSealed || !(type.Namespace?.EndsWith(".Integration.Consumers", StringComparison.Ordinal) ?? false))
            .Select(type => $"{type.FullName} is not a sealed class of an Integration.Consumers namespace")];

    private static List<string> PublisherProblems(IEnumerable<Type> publishers) =>
        [.. publishers
            .Where(type => !(type.Namespace?.EndsWith(".Integration.Publishers", StringComparison.Ordinal) ?? false))
            .Select(type => $"{type.FullName} publishes integration events outside an Integration.Publishers namespace")];

    private static List<string> UnrecordedSubscriptions(IEnumerable<Type> handlers, Func<Assembly, IModel?> modelOf) =>
        [.. handlers
            .Where(handler => modelOf(handler.Assembly)?.FindEntityType(typeof(ProcessedMessage)) is null)
            .Select(handler => $"{handler.FullName} belongs to {handler.Assembly.GetName().Name}, whose context maps no {nameof(ProcessedMessage)}")];

    private static List<string> TableProblems(IModel model, string schema)
    {
        var problems = new List<string>();
        if (model.FindEntityType(typeof(OutboxMessage)) is { } outbox)
        {
            if ((outbox.GetTableName(), outbox.GetSchema()) != (OutboxMessageConfiguration.TableName, schema))
            {
                problems.Add($"{outbox.GetSchema()}.{outbox.GetTableName()} is not {schema}.{OutboxMessageConfiguration.TableName}");
            }

            if (outbox.FindProperty(nameof(OutboxMessage.Payload))?.GetColumnType() != "json")
            {
                problems.Add($"{schema}'s outbox keeps its payload outside a json column");
            }

            if (outbox.GetIndexes().Count(index => index.GetFilter() is not null) != 2)
            {
                problems.Add($"{schema}'s outbox has not the two filtered indexes of its configuration");
            }
        }

        if (model.FindEntityType(typeof(ProcessedMessage)) is { } processed
            && ((processed.GetTableName(), processed.GetSchema()) != (ProcessedMessageConfiguration.TableName, schema) || !processed.FindPrimaryKey()!.Properties.Select(property => property.Name).SequenceEqual(ProcessedKey)))
        {
            problems.Add($"{processed.GetSchema()}.{processed.GetTableName()} is not {schema}.{ProcessedMessageConfiguration.TableName} keyed by event and subscriber");
        }

        return problems;
    }

    [GeneratedRegex(@"(^|[.-])v[0-9]+($|[.-])")]
    private static partial Regex VersionSuffix();

    public sealed record MisplacedEvent(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-misplaced";
    }

    public sealed class MisplacedSubscriber : IIntegrationEventHandler<ProbeEvents.LedgerPosted>
    {
        public Task<Result> HandleAsync(ProbeEvents.LedgerPosted integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    public sealed class MisplacedPublisher(IIntegrationEventPublisher publisher)
    {
        public IIntegrationEventPublisher Publisher { get; } = publisher;
    }
}
