using Dewiride.Erp.BuildingBlocks.Authentication.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Abstractions;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Logging;

public sealed class BoundedIdentityModelLoggerTests
{
    private const string Refusal = "IDX10223: Lifetime validation failed. The token is expired.";

    private readonly FakeLogger<BoundedIdentityModelLogger> _records = new();

    private readonly FakeTimeProvider _clock = new();

    [Fact]
    public void Log_WarningsWithinTheLimit_AllPassAsTheyWereWritten()
    {
        var logger = Logger();

        for (var i = 0; i < BoundedIdentityModelLogger.RecordsPerWindow; i++)
        {
            logger.Log(Entry(EventLogLevel.Warning, $"{Refusal} {i}"));
        }

        var records = _records.Collector.GetSnapshot();
        Assert.Equal(BoundedIdentityModelLogger.RecordsPerWindow, records.Count);
        Assert.All(records, record => Assert.Equal(LogLevel.Warning, record.Level));
        Assert.Equal($"{Refusal} 0", records[0].Message);
    }

    [Fact]
    public void Log_WarningsOverTheLimit_AreHeldBackAndCountedBeforeTheFirstOfTheNextWindow()
    {
        var logger = Logger();
        for (var i = 0; i < BoundedIdentityModelLogger.RecordsPerWindow + 3; i++)
        {
            logger.Log(Entry(EventLogLevel.Warning, Refusal));
        }

        Assert.Equal(BoundedIdentityModelLogger.RecordsPerWindow, _records.Collector.Count);

        _clock.Advance(BoundedIdentityModelLogger.Window);
        logger.Log(Entry(EventLogLevel.Error, "IDX10503: Signature validation failed."));

        var records = _records.Collector.GetSnapshot();
        Assert.Equal(BoundedIdentityModelLogger.RecordsPerWindow + 2, records.Count);
        Assert.Equal(LogLevel.Warning, records[^2].Level);
        Assert.Equal("3", records[^2].GetStructuredStateValue("Count"));
        Assert.Equal(LogLevel.Error, records[^1].Level);
        Assert.Equal("IDX10503: Signature validation failed.", records[^1].Message);
    }

    [Fact]
    public void Log_WarningsErrorsAndCriticalRecords_ShareOneAllowance()
    {
        var logger = Logger();

        for (var i = 0; i < BoundedIdentityModelLogger.RecordsPerWindow; i++)
        {
            logger.Log(Entry((i % 3) switch { 0 => EventLogLevel.Warning, 1 => EventLogLevel.Error, _ => EventLogLevel.Critical }, Refusal));
        }

        logger.Log(Entry(EventLogLevel.Critical, Refusal));

        Assert.Equal(BoundedIdentityModelLogger.RecordsPerWindow, _records.Collector.Count);
    }

    [Fact]
    public void Log_WeakerRecordsOnceTheAllowanceIsSpent_StillPass()
    {
        var logger = Logger();
        for (var i = 0; i < BoundedIdentityModelLogger.RecordsPerWindow + 1; i++)
        {
            logger.Log(Entry(EventLogLevel.Warning, Refusal));
        }

        logger.Log(Entry(EventLogLevel.Informational, "IDX10242: Security token has a valid signature."));
        logger.Log(Entry(EventLogLevel.Verbose, "IDX10239: Lifetime of the token is valid."));

        var records = _records.Collector.GetSnapshot();
        Assert.Equal(BoundedIdentityModelLogger.RecordsPerWindow + 2, records.Count);
        Assert.Equal([LogLevel.Information, LogLevel.Debug], records.TakeLast(2).Select(record => record.Level));
    }

    [Theory]
    [InlineData(EventLogLevel.Critical, LogLevel.Critical)]
    [InlineData(EventLogLevel.Error, LogLevel.Error)]
    [InlineData(EventLogLevel.Warning, LogLevel.Warning)]
    [InlineData(EventLogLevel.Informational, LogLevel.Information)]
    [InlineData(EventLogLevel.Verbose, LogLevel.Debug)]
    [InlineData(EventLogLevel.LogAlways, LogLevel.Information)]
    public void Log_Record_IsWrittenAtTheMatchingLogLevel(EventLogLevel eventLevel, LogLevel level)
    {
        Logger().Log(Entry(eventLevel, Refusal));

        Assert.Equal(level, _records.LatestRecord.Level);
    }

    [Fact]
    public void Log_RecordOfADisabledLevel_IsDroppedWithoutSpendingTheAllowance()
    {
        var logger = Logger();
        _records.ControlLevel(LogLevel.Warning, enabled: false);
        _records.ControlLevel(LogLevel.Debug, enabled: false);

        for (var i = 0; i < BoundedIdentityModelLogger.RecordsPerWindow; i++)
        {
            logger.Log(Entry(EventLogLevel.Warning, Refusal));
        }

        logger.Log(Entry(EventLogLevel.Verbose, Refusal));
        Assert.Equal(0, _records.Collector.Count);
        Assert.False(logger.IsEnabled(EventLogLevel.Warning));
        Assert.False(logger.IsEnabled(EventLogLevel.Verbose));
        Assert.True(logger.IsEnabled(EventLogLevel.Error));

        logger.Log(Entry(EventLogLevel.Error, Refusal));
        Assert.Equal(1, _records.Collector.Count);
    }

    private BoundedIdentityModelLogger Logger() => new(_records, _clock);

    private static LogEntry Entry(EventLogLevel level, string message) => new() { EventLogLevel = level, Message = message };
}
