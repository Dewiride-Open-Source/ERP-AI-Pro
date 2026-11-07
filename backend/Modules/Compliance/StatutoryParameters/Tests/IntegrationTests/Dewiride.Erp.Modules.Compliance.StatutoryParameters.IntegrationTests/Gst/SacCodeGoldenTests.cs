using System.Globalization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Gst;

// Golden values of the Scheme of Classification of Services, the Annexure to Notification No. 11/2017-Central Tax (Rate), and of
// the ERP's reading of the entry each code falls under (owner decision of 2026-11-07: each pinned value beside its instrument).
[Collection(StatutoryRows.Name)]
public sealed class SacCodeGoldenTests(ErpApiFactory factory)
{
    [Theory]
    [InlineData("9973", "2019-09-30", SacCodeLevel.Heading, null, "Leasing or rental services with or without operator", "Annexure: Scheme of Classification of Services, serial number 232")]
    [InlineData("9973", "2019-10-01", SacCodeLevel.Heading, null, "Leasing or rental services without operator", "Notification No. 20/2019-Central Tax (Rate), clause (iv)(b)")]
    [InlineData("99733", "2017-07-01", SacCodeLevel.Group, "9973", "Licensing services for the right to use intellectual property and similar products", "serial number 250")]
    [InlineData("997331", "2017-07-01", SacCodeLevel.Service, "99733", "Licensing services for the right to use computer software and databases", "serial number 251")]
    [InlineData("9983", "2017-07-01", SacCodeLevel.Heading, null, "Other professional, technical and business services", "serial number 296")]
    [InlineData("998314", "2026-11-07", SacCodeLevel.Service, "99831", "Information technology design and development services", "Notification No. 11/2017-Central Tax (Rate), Annexure")]
    [InlineData("998363", "2026-11-07", SacCodeLevel.Service, "99836", "Sale of advertising space in print media (except on commission)", "Notification No. 11/2017-Central Tax (Rate), Annexure")]
    [InlineData("998365", "2026-11-07", SacCodeLevel.Service, "99836", "Sale of internet advertising space", "Notification No. 11/2017-Central Tax (Rate), Annexure")]
    public async Task Code_OnADay_IsAsTheAnnexurePrintsIt(string code, string day, SacCodeLevel level, string? parent, string description, string instrument)
    {
        var date = DateOnly.Parse(day, CultureInfo.InvariantCulture);
        await using var scope = factory.Services.CreateAsyncScope();

        var codes = await CodesAsync(scope.ServiceProvider.GetRequiredService<IStatutoryParameters>(), date);
        var version = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync("gst.sac-codes", date, TestContext.Current.CancellationToken);

        Assert.Equal(new SacCode(code, level, parent, description), Assert.Single(codes, candidate => candidate.Code == code));
        Assert.Contains(instrument, Assert.Single(version!.Rows, row => row.Key == code).Source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Codes_OfHeading9983_AreItsNineGroupsAndFiftyNineServices()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var codes = await CodesAsync(scope.ServiceProvider.GetRequiredService<IStatutoryParameters>(), new DateOnly(2026, 11, 7));

        var groups = codes.Where(code => code.Level == SacCodeLevel.Group && code.Parent == "9983").Select(code => code.Code).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(["99831", "99832", "99833", "99834", "99835", "99836", "99837", "99838", "99839"], groups.Order(StringComparer.Ordinal));
        Assert.Equal(59, codes.Count(code => code.Level == SacCodeLevel.Service && code.Parent is { } group && groups.Contains(group)));
    }

    [Theory]
    [InlineData("2017-07-01")]
    [InlineData("2019-10-01")]
    [InlineData("2026-11-07")]
    public async Task Codes_OnEveryVersionsDate_HoldNoCodeOfGstnsListAlone(string day)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var codes = await CodesAsync(scope.ServiceProvider.GetRequiredService<IStatutoryParameters>(), DateOnly.Parse(day, CultureInfo.InvariantCulture));

        Assert.DoesNotContain(codes, code => code.Code is "998329" or "998389" or "998398");
    }

    [Theory]
    [InlineData("998314", "21(ii)")]
    [InlineData("998363", "21(i)")]
    [InlineData("998365", "21(ii)")]
    [InlineData("997331", "17(ii)")]
    [InlineData("9983", "21(ii)")]
    public async Task RateEntry_OfACode_IsTheErpsReadingOfItsDefaultEntry(string code, string entry)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var entries = await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<SacRateEntries>(new DateOnly(2026, 11, 7), TestContext.Current.CancellationToken);

        var mapped = Assert.Single(entries!.Entries, candidate => candidate.Code == code);
        Assert.Equal(entry, mapped.Entry);
        Assert.Contains("Explanation (ii)", await SourceOfMappingAsync(scope, code), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2017-07-01")]
    [InlineData("2019-10-01")]
    [InlineData("2026-11-07")]
    public async Task RateEntries_OnEveryVersionsDate_MapEverySixDigitCodeOnceAndNoEntryThatAppliesByWhatASupplyIs(string day)
    {
        var date = DateOnly.Parse(day, CultureInfo.InvariantCulture);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var services = (await CodesAsync(store, date)).Where(code => code.Level == SacCodeLevel.Service).Select(code => code.Code).Order(StringComparer.Ordinal);
        var entries = (await store.GetAsOfAsync<SacRateEntries>(date, TestContext.Current.CancellationToken))!.Entries;

        Assert.Equal(services, entries.Where(entry => entry.Code.Length == 6).Select(entry => entry.Code).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.Entry is "21(ia)" or "38");
    }

    [Fact]
    public async Task Sets_OnTheDayBeforeTheGstBegan_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        Assert.Null(await store.GetAsOfAsync<SacCodes>(new DateOnly(2017, 6, 30), TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<SacRateEntries>(new DateOnly(2017, 6, 30), TestContext.Current.CancellationToken));
    }

    private static async Task<IReadOnlyList<SacCode>> CodesAsync(IStatutoryParameters store, DateOnly day) =>
        (await store.GetAsOfAsync<SacCodes>(day, TestContext.Current.CancellationToken))?.Codes
            ?? throw new InvalidOperationException($"gst.sac-codes holds nothing for {day:yyyy-MM-dd}.");

    private static async Task<string> SourceOfMappingAsync(AsyncServiceScope scope, string code)
    {
        var version = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync("gst.sac-rate-entries", new DateOnly(2026, 11, 7), TestContext.Current.CancellationToken);

        return Assert.Single(version!.Rows, row => row.Key == code).Source;
    }
}
