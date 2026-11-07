using System.Globalization;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Persistence;
using Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.PublicApi;

// Every row of the seed file is asked for on the day before it starts, its first day, its last day and the day after it
// ends, and the answer must be the row the file holds for that code and day, so the dates come from the file, never from
// the test.
[Collection(ReferenceRowReaders.Name)]
public sealed class IndianStateBoundaryTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    public static TheoryData<string, string, string?> Boundaries()
    {
        var file = IndianStateSourceFile.Read();
        var data = new TheoryData<string, string, string?>();
        foreach (var row in file.States)
        {
            List<DateOnly> days = [row.ValidFrom.AddDays(-1), row.ValidFrom];
            if (row.ValidUntil is { } until)
            {
                days.Add(until);
                days.Add(until.AddDays(1));
            }

            foreach (var day in days)
            {
                var expected = file.States.SingleOrDefault(other =>
                    other.GstCode == row.GstCode && other.ValidFrom <= day && (other.ValidUntil is null || other.ValidUntil >= day));
                data.Add(row.GstCode, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), expected?.Name);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task FindAsync_DayAtTheEdgeOfARow_AnswersTheRowTheFileHoldsForThatDay(string code, string day, string? expectedName)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var state = await scope.ServiceProvider.GetRequiredService<IIndianStates>()
            .FindAsync(code, DateOnly.Parse(day, CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);

        Assert.Equal(expectedName, state?.Name);
    }
}
