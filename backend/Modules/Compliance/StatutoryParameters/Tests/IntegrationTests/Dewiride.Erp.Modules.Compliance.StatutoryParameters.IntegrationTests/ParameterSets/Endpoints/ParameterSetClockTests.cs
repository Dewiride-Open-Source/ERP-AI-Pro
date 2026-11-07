using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.ParameterSets.Endpoints;

// A version starts at midnight in India, 18:30 UTC of the day before, whatever the server's own time zone; the date of the
// version comes from its seed file. The host starts on today's clock, which its sign-in certificate is valid on, and the clock is
// then set back to the night before the version. The clock answers in UTC, as every TimeProvider does, so a handler that took
// the date of the UTC instant instead of India's would show the new version five and a half hours late and fail here.
[Collection(StatutoryRows.Name)]
public sealed class ParameterSetClockTests
{
    private const string CentralTaxRates = "gst.central-tax-rates";

    [Fact]
    public async Task Sets_FromOneSecondBeforeMidnightInIndiaToMidnight_MoveTheVersionInForceToTheUpcomingOne()
    {
        var file = SeedFileSets.Of(CentralTaxRates);
        var versions = file.Versions.Select(version => version.From).Order().ToList();
        var midnight = IndianStandardTime.StartOfIstDay(versions[^1]);
        var clock = new SettableClock(TimeProvider.System.GetUtcNow());
        await using var factory = new ErpApiFactory().WithTestServices(services => services.AddSingleton<TimeProvider>(clock));
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        clock.Now = midnight.AddSeconds(-1);
        var before = await SummaryAsync(client);
        var beforeSet = await ParameterSetsApi.GetAsync(client, CentralTaxRates);
        clock.Now = midnight;
        var after = await SummaryAsync(client);
        var afterSet = await ParameterSetsApi.GetAsync(client, CentralTaxRates);

        Assert.Equal((new TimeSpan(18, 30, 0), TimeSpan.Zero), (clock.GetUtcNow().TimeOfDay, clock.GetUtcNow().Offset));
        Assert.Equal((versions[^2], versions[^1].AddDays(-1)), (before.Current!.From, before.Current.Until));
        Assert.NotNull(before.Next);
        var upcoming = beforeSet.Versions.Single(version => version.From == versions[^1]);
        Assert.Equal(
            (upcoming.From, upcoming.Until, upcoming.AddedCount, upcoming.ChangedCount, upcoming.RemovedCount),
            (before.Next.From, before.Next.Until, before.Next.AddedCount, before.Next.ChangedCount, before.Next.RemovedCount));
        Assert.Equal(file.Versions.Single(version => version.From == versions[^1]).Instruments, before.Next.Instruments);
        Assert.Equal((versions[^1], (DateOnly?)null), (after.Current!.From, after.Current.Until));
        Assert.Null(after.Next);
        Assert.Equal((versions[^2], versions[^2]), (beforeSet.CurrentFrom, beforeSet.Shown!.From));
        Assert.Equal((versions[^1], versions[^1]), (afterSet.CurrentFrom, afterSet.Shown!.From));
    }

    private static async Task<ParameterSetSummaryResponse> SummaryAsync(HttpClient client) =>
        Assert.Single((await ParameterSetsApi.ListAsync(client)).Sets, set => set.Key == CentralTaxRates);

    private sealed class SettableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }
}
