using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.ParameterSets.Endpoints;

// A version starts at midnight in India, 18:30 UTC of the day before, whatever the server's own time zone; the date of the
// version comes from its seed file. The host starts on today's clock, which its sign-in certificate is valid on, and the clock is
// then set back to the night before the version.
[Collection(StatutoryRows.Name)]
public sealed class ParameterSetClockTests(ErpApiFactory seeded)
{
    private const string CentralTaxRates = "gst.central-tax-rates";

    [Fact]
    public async Task List_FromOneSecondBeforeMidnightInIndiaToMidnight_MovesTheVersionInForceToTheNewOne()
    {
        var versions = await VersionDatesAsync();
        var midnight = IndianStandardTime.StartOfIstDay(versions[^1]);
        var clock = new SettableClock(TimeProvider.System.GetUtcNow());
        await using var factory = new ErpApiFactory().WithTestServices(services => services.AddSingleton<TimeProvider>(clock));
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        clock.Now = midnight.AddSeconds(-1);
        var before = await CurrentAsync(client);
        clock.Now = midnight;
        var after = await CurrentAsync(client);

        Assert.Equal(new TimeSpan(18, 30, 0), midnight.UtcDateTime.TimeOfDay);
        Assert.Equal(versions[^2], before.From);
        Assert.Equal(versions[^1].AddDays(-1), before.Until);
        Assert.Equal(versions[^1], after.From);
        Assert.Null(after.Until);
    }

    private static async Task<ParameterVersionSummaryResponse> CurrentAsync(HttpClient client)
    {
        var set = Assert.Single((await ParameterSetsApi.ListAsync(client)).Sets, set => set.Key == CentralTaxRates);
        Assert.NotNull(set.Current);

        return set.Current;
    }

    private async Task<List<DateOnly>> VersionDatesAsync()
    {
        await using var scope = seeded.Services.CreateAsyncScope();
        var states = await scope.ServiceProvider.GetRequiredService<IIndianStates>().ListAsync(null, TestContext.Current.CancellationToken);
        var file = Assert.Single(StatutorySeedFiles.Read(StatutorySeedFiles.Embedded(), states), set => set.Set.Key == CentralTaxRates);

        return [.. file.Versions.Select(version => version.From).Order()];
    }

    private sealed class SettableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
