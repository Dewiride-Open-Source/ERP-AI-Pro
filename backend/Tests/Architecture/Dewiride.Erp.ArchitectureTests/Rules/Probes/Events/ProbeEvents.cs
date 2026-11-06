using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Events;

namespace Dewiride.Erp.ArchitectureTests.Rules.Probes.Events;

// Events placed as a module's Contracts place theirs, one keeping every rule and one breaking each, for IntegrationEventTests.
public static class ProbeEvents
{
    public enum ProbeKind
    {
        Opening,
        Closing,
    }

    public sealed record LedgerPosted(Guid LedgerId, int Lines, bool Balanced, DateOnly On, DateTimeOffset At, DateOnly? ReversesOn, IReadOnlyList<Guid> EntryIds, ErpRole PostedAs, Guid? ApprovedBy = null) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-posted";
    }

    public record LedgerOpened(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-opened";
    }

    public sealed class LedgerArchived(Guid ledgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-archived";

        public Guid LedgerId { get; } = ledgerId;
    }

    public sealed record LedgerClosedElsewhere(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.other.ledger-closed";
    }

    public sealed record LedgerPostedV2(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-posted-v2";
    }

    public sealed record LedgerNoted(Guid LedgerId, string Note, long Total, decimal Amount, ProbeKind Kind, IReadOnlyList<string> Tags) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-noted";
    }

    public sealed record LedgerCommented(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-commented";

        [JsonInclude]
        internal string? Comment { get; init; }
    }

    public sealed record LedgerRelabelled([property: JsonConverter(typeof(GuidAsLabelConverter))] Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-relabelled";
    }

    [JsonConverter(typeof(LedgerRewrittenConverter))]
    public sealed record LedgerRewritten(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-rewritten";
    }

    public sealed record LedgerExtended(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-extended";

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? Rest { get; init; }
    }

    public sealed record LedgerMisnamed(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "Probe.Ledgers.Misnamed";
    }

    public sealed record LedgerPostedAgain(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-posted";
    }

    public sealed class GuidAsLabelConverter : JsonConverter<Guid>
    {
        public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Guid.Empty;

        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) => writer?.WriteStringValue("any label");
    }

    public sealed class LedgerRewrittenConverter : JsonConverter<LedgerRewritten>
    {
        public override LedgerRewritten Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(Guid.Empty);

        public override void Write(Utf8JsonWriter writer, LedgerRewritten value, JsonSerializerOptions options) => writer?.WriteStringValue("any text");
    }

    internal sealed record LedgerHidden(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-hidden";
    }
}
