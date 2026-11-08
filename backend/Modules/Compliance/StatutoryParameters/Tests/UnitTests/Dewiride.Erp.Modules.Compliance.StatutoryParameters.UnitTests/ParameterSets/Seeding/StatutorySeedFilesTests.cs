using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets.Seeding;

public sealed class StatutorySeedFilesTests
{
    [Fact]
    public void Embedded_SeedFiles_AreTheGstDirectTaxAndLabourSetsEachUnderItsKey()
    {
        var files = StatutorySeedFiles.Embedded();

        Assert.Equal(
            [
                "bonus.prior-charges.json",
                "bonus.rules.json",
                "bonus.state-wage-limit-readings.json",
                "bonus.wage-limits.json",
                "companies.schedule-ii-limits.json",
                "companies.schedule-ii-useful-lives.json",
                "esi.average-daily-wage-divisors.json",
                "esi.contribution-due-dates.json",
                "esi.contribution-exemptions.json",
                "esi.contribution-periods.json",
                "esi.contribution-rates.json",
                "esi.coverage-wage-limits.json",
                "gratuity.rules.json",
                "gst.central-tax-rates.json",
                "gst.integrated-tax-rates.json",
                "gst.quarterly-return-states.json",
                "gst.return-due-dates.json",
                "gst.return-thresholds.json",
                "gst.sac-codes.json",
                "gst.sac-rate-entries.json",
                "gst.union-territory-tax-rates.json",
                "income-tax.advance-tax-instalments.json",
                "income-tax.advance-tax-interest.json",
                "income-tax.company-rates.json",
                "income-tax.company-surcharge.json",
                "income-tax.depreciation-allowances.json",
                "income-tax.depreciation-notes.json",
                "income-tax.depreciation-rates.json",
                "income-tax.health-and-education-cess.json",
                "income-tax.minimum-alternate-tax.json",
                "income-tax.tds-crosswalk.json",
                "income-tax.tds-declarations.json",
                "income-tax.tds-entries.json",
                "income-tax.tds-higher-rates.json",
                "income-tax.tds-no-deduction-cases.json",
                "income-tax.tds-rates-in-force-readings.json",
                "income-tax.tds-rates-in-force.json",
                "income-tax.tds-rates.json",
                "income-tax.tds-surcharge-and-cess.json",
                "income-tax.tds-terms.json",
                "income-tax.tds-threshold-limits.json",
                "labour-codes.wage-definition.json",
                "labour-welfare-fund.contributions.json",
                "labour-welfare-fund.coverage.json",
                "labour-welfare-fund.due-days.json",
                "professional-tax.exemptions.json",
                "professional-tax.levies.json",
                "professional-tax.salary-slabs.json",
                "provident-fund.charges.json",
                "provident-fund.contribution-rates.json",
                "provident-fund.due-dates.json",
                "provident-fund.membership-limits.json",
                "provident-fund.rounding.json",
                "provident-fund.ten-per-cent-establishments.json",
            ],
            files.Select(file => file.FileName));
    }

    [Fact]
    public void Read_EmbeddedSeedFiles_ReadsEveryOneWhole()
    {
        var sets = StatutorySeedFiles.Read(StatutorySeedFiles.Embedded(), SeedProbes.AnyStateCode);

        Assert.Equal(StatutorySeedFiles.Embedded().Select(file => file.FileName), sets.Select(set => set.Set.Key + StatutorySeedFiles.FileExtension));
        Assert.All(sets, set => Assert.NotEmpty(set.Versions));
        Assert.All(sets, set => Assert.NotEmpty(set.Rows));
    }

    [Fact]
    public void Read_ProbeFile_ReadsItsSetVersionsRowsAndReferences()
    {
        var seeded = Assert.Single(SeedProbes.Read(SeedProbes.Source(SeedProbes.File())));

        Assert.Equal(SeedProbes.Key, seeded.Set.Key);
        Assert.Equal([TestSets.Day("2017-07-01"), TestSets.Day("2019-10-01")], seeded.Versions.Select(version => version.From));
        Assert.Equal(["a", "a", "b"], seeded.Rows.Select(row => row.RowKey));
        Assert.Equal("2.5", seeded.Rows[2].Cells["rate"]);
        Assert.Null(seeded.Rows[2].Cells["state"]);
        Assert.Equal([SeedProbes.Key], seeded.References["parent"]);
        Assert.Equal((0m, 100m), (seeded.Set.Columns.Find("rate")!.Minimum, seeded.Set.Columns.Find("rate")!.Maximum));
    }

    [Theory]
    [InlineData("\"basis\": \"law\",", "\"basis\": \"law\", \"notes\": \"A member the shape does not know\",")]
    [InlineData("\"title\": \"Probe rates\",", "\"title\": \"Probe rates\", \"title\": \"Written twice\",")]
    [InlineData("\"minimum\": 0", "\"minimum\": \"0\"")]
    [InlineData("\"rate\": 9,", "\"rate\": \"9\",")]
    [InlineData("\"entry\": \"b\",", "\"entry\": \"b\", \"entry\": \"c\",")]
    [InlineData("\"entry\": \"b\", \"rate\": 2.5, \"state\": null,", "\"entry\": \"b\", \"rate\": 2.5,")]
    [InlineData("\"parent\": \"a\" }", "\"parent\": \"a\", \"extra\": null }")]
    [InlineData("\"rate\": 2.5,", "\"rate\": 2.50001,")]
    [InlineData("\"rate\": 18,", "\"rate\": 100.5,")]
    [InlineData("\"kind\": \"percent\"", "\"kind\": \"Percent\"")]
    [InlineData("\"basis\": \"law\"", "\"basis\": \"Law\"")]
    [InlineData("\"key\": true }", "\"key\": true, \"optional\": false }")]
    [InlineData("\"until\": \"2019-09-30\",", "\"until\": \"2019-09-29\",")]
    [InlineData("{ \"from\": \"2019-10-01\", \"instruments\": [\"Probe instrument two\"] }", "{ \"from\": \"2019-10-01\", \"instruments\": [\"Probe instrument two\"] }, { \"from\": \"2021-01-01\", \"instruments\": [\"Probe instrument three\"] }")]
    [InlineData("\"from\": \"2019-10-01\", \"source\": \"Probe source two\"", "\"from\": \"2019-09-30\", \"source\": \"Probe source two\"")]
    [InlineData("\"parent\": \"a\" }", "\"parent\": \"c\" }")]
    [InlineData("\"rate\": 9, \"state\": \"27\"", "\"rate\": 9, \"state\": \"2.7\"")]
    [InlineData("\"from\": \"2017-07-01\", \"instruments\"", "\"from\": \"2017-07-32\", \"instruments\"")]
    [InlineData("\"source\": \"Probe source one\",", "\"source\": \"Probe  source one\",")]
    [InlineData("\"instruments\": [\"Probe instrument one\"]", "\"instruments\": []")]
    public void Read_ProbeFileBrokenInOnePlace_IsRefusedWhole(string original, string replacement)
    {
        var refusal = SeedProbes.Refusal(SeedProbes.Source(SeedProbes.Broken(original, replacement)));

        Assert.Contains(SeedProbes.Key + StatutorySeedFiles.FileExtension, refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ProbeFileUnderAnotherName_IsRefused()
    {
        var refusal = SeedProbes.Refusal(SeedProbes.Source(SeedProbes.File(), "test.another-name.json"));

        Assert.Contains("is not the file's name", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ReferenceToASetNoFileHolds_IsRefused()
    {
        var refusal = SeedProbes.Refusal(SeedProbes.Source(SeedProbes.Broken($"\"references\": [\"{SeedProbes.Key}\"]", "\"references\": [\"test.no-such-set\"]")));

        Assert.Contains("references test.no-such-set, which no seed file holds", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_StateCodeNotInForceOnEveryDayOfItsRow_IsRefused()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => StatutorySeedFiles.Read(
            [SeedProbes.Source(SeedProbes.File())],
            [new IndianStateEntry("27", "Probe territory", IndianStateKind.State, TestSets.Day("2017-07-02"), null)])).Message;

        Assert.Contains("GST State code 27 is not in force on every day from 2017-07-01 to 2019-09-30", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ValidFileBesideADefectiveOne_IsRefusedWholeNamingOnlyTheDefectiveOne()
    {
        var refusal = SeedProbes.Refusal(
            SeedProbes.Source(SeedProbes.File("test.probe-valid"), "test.probe-valid.json"),
            SeedProbes.Source(SeedProbes.Broken("\"rate\": 18,", "\"rate\": \"18\",")));

        Assert.Contains(SeedProbes.Key, refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("test.probe-valid.json", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_TwoDefectiveFiles_NamesEveryDefect()
    {
        var refusal = SeedProbes.Refusal(
            SeedProbes.Source(SeedProbes.File("test.probe-first").Replace("\"rate\": 18,", "\"rate\": 180,", StringComparison.Ordinal), "test.probe-first.json"),
            SeedProbes.Source(SeedProbes.File("test.probe-second").Replace("\"until\": \"2019-09-30\",", "\"until\": \"2019-10-01\",", StringComparison.Ordinal), "test.probe-second.json"));

        Assert.Contains("test.probe-first.json", refusal, StringComparison.Ordinal);
        Assert.Contains("test.probe-second.json", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_TwoFilesOfOneSet_IsRefused()
    {
        var refusal = SeedProbes.Refusal(SeedProbes.Source(SeedProbes.File()), SeedProbes.Source(SeedProbes.File()));

        Assert.Contains("2 seed files hold the set", refusal, StringComparison.Ordinal);
    }
}
