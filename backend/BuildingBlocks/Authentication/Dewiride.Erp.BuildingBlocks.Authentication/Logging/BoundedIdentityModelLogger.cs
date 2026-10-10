using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Abstractions;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Logging;

// IdentityModel writes a Warning or an Error record for every token it refuses, so a client sending refused tokens could fill
// the log; every refused token is a security event instead (BearerTokenEvents). At most RecordsPerWindow of its records at
// Warning or above pass per Window, and the next one that passes is preceded by how many were held back. Its weaker records
// pass as they are, subject to the configured log levels, as through the adapter this one stands in for.
internal sealed partial class BoundedIdentityModelLogger(ILogger<BoundedIdentityModelLogger> logger, TimeProvider timeProvider) : IIdentityLogger
{
    public const int RecordsPerWindow = 10;

    private readonly Lock _gate = new();

    private DateTimeOffset _windowEndsAt;

    private int _passed;

    private int _heldBack;

    public static TimeSpan Window { get; } = TimeSpan.FromMinutes(1);

    public bool IsEnabled(EventLogLevel eventLogLevel) => logger.IsEnabled(LevelOf(eventLogLevel));

    public void Log(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var level = LevelOf(entry.EventLogLevel);
        if (!logger.IsEnabled(level))
        {
            return;
        }

        var held = 0;
        if (level >= LogLevel.Warning && !TryPass(out held))
        {
            return;
        }

        if (held > 0)
        {
            LogHeldBack(logger, held);
        }

        LogRecord(logger, level, entry.Message);
    }

    private bool TryPass(out int held)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (now >= _windowEndsAt)
            {
                _windowEndsAt = now + Window;
                _passed = 0;
            }

            held = 0;
            if (_passed >= RecordsPerWindow)
            {
                _heldBack++;
                return false;
            }

            _passed++;
            held = _heldBack;
            _heldBack = 0;
            return true;
        }
    }

    private static LogLevel LevelOf(EventLogLevel level) =>
        level switch
        {
            EventLogLevel.Critical => LogLevel.Critical,
            EventLogLevel.Error => LogLevel.Error,
            EventLogLevel.Warning => LogLevel.Warning,
            EventLogLevel.Verbose => LogLevel.Debug,
            _ => LogLevel.Information,
        };

    [LoggerMessage(Message = "{IdentityModelRecord}")]
    private static partial void LogRecord(ILogger logger, LogLevel level, string? identityModelRecord);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Held back {Count} IdentityModel records over the limit of the last window")]
    private static partial void LogHeldBack(ILogger logger, int count);
}
