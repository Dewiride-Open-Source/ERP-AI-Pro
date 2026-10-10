using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;

// A security event that names no actor can be caused by anyone, from any number of addresses, and is kept for the retention
// period, so at most EventsPerWindow of them are written per Window across the process, however many addresses send them;
// the rest are counted. The first one held back in a window is logged, and the next one written is preceded by how many were
// held back. An event that names an actor is never held back.
internal sealed partial class AnonymousSecurityEventBudget(TimeProvider timeProvider, ILogger<AnonymousSecurityEventBudget> logger)
{
    public const int EventsPerWindow = 60;

    private readonly Lock _gate = new();

    private DateTimeOffset _windowEndsAt;

    private int _written;

    private int _heldBack;

    private bool _limitLogged;

    public static TimeSpan Window { get; } = TimeSpan.FromMinutes(1);

    public bool TryTake()
    {
        bool taken;
        var limitReached = false;
        var heldBack = 0;
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (now >= _windowEndsAt)
            {
                _windowEndsAt = now + Window;
                _written = 0;
                _limitLogged = false;
            }

            taken = _written < EventsPerWindow;
            if (taken)
            {
                _written++;
                heldBack = _heldBack;
                _heldBack = 0;
            }
            else
            {
                _heldBack++;
                limitReached = !_limitLogged;
                _limitLogged = true;
            }
        }

        if (limitReached)
        {
            LogLimitReached(logger, EventsPerWindow);
        }

        if (heldBack > 0)
        {
            LogHeldBack(logger, heldBack);
        }

        return taken;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reached the limit of {Limit} security events without an actor a minute; the others of this minute are counted, not recorded")]
    private static partial void LogLimitReached(ILogger logger, int limit);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Held back {Count} security events without an actor over the limit")]
    private static partial void LogHeldBack(ILogger logger, int count);
}
