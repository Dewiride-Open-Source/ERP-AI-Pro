using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Codes;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Labour;

// Golden values of the definition of wages in section 2(y) of the Code on Wages, 2019 and section 2(88) of the Code on Social
// Security, 2020, clause by clause, each pinned beside its Code so that a wrong edit of a seed file fails a test (ADR-0063). Both
// definitions hold from 1 April 2026, where the ERP's holding starts.
[Collection(StatutoryRows.Name)]
public sealed class WageDefinitionGoldenTests(ErpApiFactory factory)
{
    private const string DefinitionKey = "labour-codes.wage-definition";

    private const string CodeOnWagesDefinition = "Code on Wages, 2019 (No. 29 of 2019), 8 August 2019, Gazette of India, Extraordinary, Part II, Section 1, No. 48: section 2(y)";

    private const string CodeOnSocialSecurityDefinition = "Code on Social Security, 2020 (No. 36 of 2020), 28 September 2020, Gazette of India, Extraordinary, Part II, Section 1, No. 61: section 2(88)";

    private static readonly DateOnly FirstDay = new(2026, 4, 1);

    [Theory]
    [InlineData("cw-2(y)-includes(i)", LabourCode.CodeOnWages, WageTreatment.Included, null, null, "and includes,— (i) basic pay;", "Code on Wages, 2019, section 2(y), inclusion (i)")]
    [InlineData("cw-2(y)-includes(ii)", LabourCode.CodeOnWages, WageTreatment.Included, null, null, "and includes,— (ii) dearness allowance; and", "Code on Wages, 2019, section 2(y), inclusion (ii)")]
    [InlineData("cw-2(y)-includes(iii)", LabourCode.CodeOnWages, WageTreatment.Included, null, null, "and includes,— (iii) retaining allowance, if any,", "Code on Wages, 2019, section 2(y), inclusion (iii)")]
    [InlineData("cw-2(y)-excludes(a)", LabourCode.CodeOnWages, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (a) any bonus payable under any law for the time being in force, which does not form part of the remuneration payable under the terms of employment;", "Code on Wages, 2019, section 2(y), exclusion (a)")]
    [InlineData("cw-2(y)-excludes(b)", LabourCode.CodeOnWages, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (b) the value of any house-accommodation, or of the supply of light, water, medical attendance or other amenity or of any service excluded from the computation of wages by a general or special order of the appropriate Government;", "Code on Wages, 2019, section 2(y), exclusion (b)")]
    [InlineData("cw-2(y)-excludes(c)", LabourCode.CodeOnWages, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (c) any contribution paid by the employer to any pension or provident fund, and the interest which may have accrued thereon;", "Code on Wages, 2019, section 2(y), exclusion (c)")]
    [InlineData("cw-2(y)-excludes(d)", LabourCode.CodeOnWages, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (d) any conveyance allowance or the value of any travelling concession;", "Code on Wages, 2019, section 2(y), exclusion (d)")]
    [InlineData("cw-2(y)-excludes(e)", LabourCode.CodeOnWages, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (e) any sum paid to the employed person to defray special expenses entailed on him by the nature of his employment;", "Code on Wages, 2019, section 2(y), exclusion (e)")]
    [InlineData("cw-2(y)-excludes(f)", LabourCode.CodeOnWages, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (f) house rent allowance;", "Code on Wages, 2019, section 2(y), exclusion (f)")]
    [InlineData("cw-2(y)-excludes(g)", LabourCode.CodeOnWages, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (g) remuneration payable under any award or settlement between the parties or order of a court or Tribunal;", "Code on Wages, 2019, section 2(y), exclusion (g)")]
    [InlineData("cw-2(y)-excludes(h)", LabourCode.CodeOnWages, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (h) any overtime allowance;", "Code on Wages, 2019, section 2(y), exclusion (h)")]
    [InlineData("cw-2(y)-excludes(i)", LabourCode.CodeOnWages, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (i) any commission payable to the employee;", "Code on Wages, 2019, section 2(y), exclusion (i)")]
    [InlineData("cw-2(y)-excludes(j)", LabourCode.CodeOnWages, WageTreatment.Excluded, null, null, "but does not include— (j) any gratuity payable on the termination of employment;", "Code on Wages, 2019, section 2(y), exclusion (j)")]
    [InlineData("cw-2(y)-excludes(k)", LabourCode.CodeOnWages, WageTreatment.Excluded, null, null, "but does not include— (k) any retrenchment compensation or other retirement benefit payable to the employee or any ex gratia payment made to him on the termination of employment:", "Code on Wages, 2019, section 2(y), exclusion (k)")]
    [InlineData("cw-2(y)-proviso-1", LabourCode.CodeOnWages, WageTreatment.Rule, "50", WageDefinitionBound.MoreThan, "Provided that, for calculating the wages under this clause, if payments made by the employer to the employee under clauses (a) to (i) exceeds one-half, or such other per cent. as may be notified by the Central Government, of the all remuneration calculated under this clause, the amount which exceeds such one-half, or the per cent. so notified, shall be deemed as remuneration", "Code on Wages, 2019, section 2(y), first proviso")]
    [InlineData("cw-2(y)-proviso-2", LabourCode.CodeOnWages, WageTreatment.Rule, null, null, "the emoluments specified in clauses (d), (f), (g) and (h) shall be taken for computation of wage.", "Code on Wages, 2019, section 2(y), second proviso")]
    [InlineData("cw-2(y)-explanation", LabourCode.CodeOnWages, WageTreatment.Rule, "15", WageDefinitionBound.AtMost, "the value of such remuneration in kind which does not exceed fifteen per cent. of the total wages payable to him, shall be deemed to form part of the wages of such employee;", "Code on Wages, 2019, section 2(y), Explanation")]
    [InlineData("css-2(88)-includes(a)", LabourCode.CodeOnSocialSecurity, WageTreatment.Included, null, null, "and includes,— (a) basic pay;", "Code on Social Security, 2020, section 2(88), inclusion (a)")]
    [InlineData("css-2(88)-includes(b)", LabourCode.CodeOnSocialSecurity, WageTreatment.Included, null, null, "and includes,— (b) dearness allowance; and", "Code on Social Security, 2020, section 2(88), inclusion (b)")]
    [InlineData("css-2(88)-includes(c)", LabourCode.CodeOnSocialSecurity, WageTreatment.Included, null, null, "and includes,— (c) retaining allowance, if any,", "Code on Social Security, 2020, section 2(88), inclusion (c)")]
    [InlineData("css-2(88)-excludes(a)", LabourCode.CodeOnSocialSecurity, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (a) any bonus payable under any law for the time being in force, which does not form part of the remuneration payable under the terms of employment;", "Code on Social Security, 2020, section 2(88), exclusion (a)")]
    [InlineData("css-2(88)-excludes(b)", LabourCode.CodeOnSocialSecurity, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (b) the value of any house-accommodation, or of the supply of light, water, medical attendance or other amenity or of any service excluded from the computation of wages by a general or special order of the appropriate Government;", "Code on Social Security, 2020, section 2(88), exclusion (b)")]
    [InlineData("css-2(88)-excludes(c)", LabourCode.CodeOnSocialSecurity, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (c) any contribution paid by the employer to any pension or provident fund, and the interest which may have accrued thereon;", "Code on Social Security, 2020, section 2(88), exclusion (c)")]
    [InlineData("css-2(88)-excludes(d)", LabourCode.CodeOnSocialSecurity, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (d) any conveyance allowance or the value of any travelling concession;", "Code on Social Security, 2020, section 2(88), exclusion (d)")]
    [InlineData("css-2(88)-excludes(e)", LabourCode.CodeOnSocialSecurity, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (e) any sum paid to the employed person to defray special expenses entailed on him by the nature of his employment;", "Code on Social Security, 2020, section 2(88), exclusion (e)")]
    [InlineData("css-2(88)-excludes(f)", LabourCode.CodeOnSocialSecurity, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (f) house rent allowance;", "Code on Social Security, 2020, section 2(88), exclusion (f)")]
    [InlineData("css-2(88)-excludes(g)", LabourCode.CodeOnSocialSecurity, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (g) remuneration payable under any award or settlement between the parties or order of a court or Tribunal;", "Code on Social Security, 2020, section 2(88), exclusion (g)")]
    [InlineData("css-2(88)-excludes(h)", LabourCode.CodeOnSocialSecurity, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (h) any overtime allowance;", "Code on Social Security, 2020, section 2(88), exclusion (h)")]
    [InlineData("css-2(88)-excludes(i)", LabourCode.CodeOnSocialSecurity, WageTreatment.ExcludedWithinLimit, null, null, "but does not include— (i) any commission payable to the employee;", "Code on Social Security, 2020, section 2(88), exclusion (i)")]
    [InlineData("css-2(88)-excludes(j)", LabourCode.CodeOnSocialSecurity, WageTreatment.Excluded, null, null, "but does not include— (j) any gratuity payable on the termination of employment;", "Code on Social Security, 2020, section 2(88), exclusion (j)")]
    [InlineData("css-2(88)-excludes(k)", LabourCode.CodeOnSocialSecurity, WageTreatment.Excluded, null, null, "but does not include— (k) any retrenchment compensation or other retirement benefit payable to the employee or any ex gratia payment made to him on the termination of employment, under any law for the time being in force:", "Code on Social Security, 2020, section 2(88), exclusion (k)")]
    [InlineData("css-2(88)-proviso-1", LabourCode.CodeOnSocialSecurity, WageTreatment.Rule, "50", WageDefinitionBound.MoreThan, "Provided that for calculating the wages under this clause, if payments made by the employer to the employee under sub-clauses (a) to (i) exceeds one-half, or such other per cent. as may be notified by the Central Government, of the all remuneration calculated under this clause, the amount which exceeds such one-half, or the per cent. so notified, shall be deemed as remuneration", "Code on Social Security, 2020, section 2(88), first proviso")]
    [InlineData("css-2(88)-proviso-2", LabourCode.CodeOnSocialSecurity, WageTreatment.Rule, null, null, "the emoluments specified in sub-clauses (d), (f), (g) and (h) shall be taken for computation of wage.", "Code on Social Security, 2020, section 2(88), second proviso")]
    [InlineData("css-2(88)-explanation", LabourCode.CodeOnSocialSecurity, WageTreatment.Rule, "15", WageDefinitionBound.AtMost, "the value of such remuneration in kind which does not exceed fifteen per cent. of the total wages payable to him, shall be deemed to form part of the wages of such employee;", "Code on Social Security, 2020, section 2(88), Explanation")]
    public async Task Clause_OfEachDefinition_IsTheTreatmentAndFigureItsCodeWrites(string clause, LabourCode labourCode, WageTreatment treatment, string? percent, WageDefinitionBound? bound, string words, string source)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var clauses = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<WageDefinition>(FirstDay, TestContext.Current.CancellationToken))!.Clauses;

        var found = Assert.Single(clauses, candidate => candidate.Clause == clause);
        Assert.Equal((labourCode, treatment, Percent(percent), bound), (found.LabourCode, found.Treatment, found.Percent, found.Bound));
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Equal(source, await SourceAsync(scope, clause));
    }

    [Theory]
    [InlineData(LabourCode.CodeOnWages)]
    [InlineData(LabourCode.CodeOnSocialSecurity)]
    public async Task Clauses_OfEachCode_IncludeThreeAndExcludeElevenOfWhichNineCountAgainstTheLimit(LabourCode labourCode)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var clauses = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<WageDefinition>(FirstDay, TestContext.Current.CancellationToken))!.Clauses
            .Where(clause => clause.LabourCode == labourCode)
            .ToList();

        Assert.Equal(
            (3, 9, 2, 3),
            (clauses.Count(clause => clause.Treatment == WageTreatment.Included), clauses.Count(clause => clause.Treatment == WageTreatment.ExcludedWithinLimit), clauses.Count(clause => clause.Treatment == WageTreatment.Excluded), clauses.Count(clause => clause.Treatment == WageTreatment.Rule)));
    }

    [Theory]
    [InlineData("2026-03-31", null)]
    [InlineData("2026-04-01", 34)]
    public async Task Definition_OnTheFirstDayOfItsVersionAndTheDayBefore_HoldsItsClausesFromThatDayAndNothingBefore(string day, int? clauses)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<WageDefinition>(Day(day), TestContext.Current.CancellationToken);

        Assert.Equal(clauses, set?.Clauses.Count);
    }

    [Fact]
    public async Task Version_OfTheDefinition_IsItsOneVersionCitingBothCodesInOrder()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(DefinitionKey, FirstDay, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((ParameterSetBasis.Law, ParameterAsOf.WageDate), (set.Basis, set.AsOf));
        var version = Assert.Single(set.Versions);
        Assert.Equal((FirstDay, (DateOnly?)null, 34, 0, 0), (version.From, version.Until, version.AddedCount, version.ChangedCount, version.RemovedCount));
        Assert.Equal(2, version.Instruments.Count);
        Assert.StartsWith(CodeOnWagesDefinition, version.Instruments[0], StringComparison.Ordinal);
        Assert.StartsWith(CodeOnSocialSecurityDefinition, version.Instruments[1], StringComparison.Ordinal);
    }

    private static DateOnly Day(string day) => DateOnly.Parse(day, CultureInfo.InvariantCulture);

    private static Percentage? Percent(string? value) => value is null ? null : new Percentage(decimal.Parse(value, CultureInfo.InvariantCulture));

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string clause)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(DefinitionKey, FirstDay, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == clause).Source;
    }
}
