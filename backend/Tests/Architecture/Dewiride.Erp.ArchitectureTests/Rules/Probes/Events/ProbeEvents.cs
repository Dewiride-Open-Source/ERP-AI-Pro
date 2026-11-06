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

    public sealed record LedgerPosted(Guid LedgerId, int Lines, bool Balanced, DateOnly On, DateTimeOffset At, DateOnly? ReversesOn, IReadOnlyList<Guid> EntryIds, Guid? ApprovedBy = null) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-posted";
    }

    public record LedgerOpened(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-opened";
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

    public sealed record LedgerMisnamed(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "Probe.Ledgers.Misnamed";
    }

    public sealed record LedgerPostedAgain(Guid LedgerId) : IIntegrationEvent
    {
        public static string Name => "probe.ledgers.ledger-posted";
    }
}
