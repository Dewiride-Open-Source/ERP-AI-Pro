using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Rules;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Documents;
using Dewiride.Erp.Modules.Platform.Numbering.Integration;

namespace Dewiride.Erp.Modules.Platform.Numbering.UnitTests.Integration;

// The clauses here are made up, so that no statutory value is written in a test that is not the set's golden test.
public sealed class GstSerialNumberRulesTests
{
    private static readonly GstDocumentSerialNumbers Set = new(
        new StatutoryParameterVersion(new DateOnly(2026, 4, 1), null, ["A test instrument"]),
        [
            new GstDocumentSerialNumber("1(a)", "Test receipt", 9, "/", "a test clause"),
            new GstDocumentSerialNumber("2(b)", "Test challan", 11, "-/", "another test clause"),
        ]);

    [Fact]
    public async Task RuleFor_AClauseTheYearsSetHolds_IsItsLengthAndCharacters()
    {
        var parameters = new AnsweringParameters(Set);

        var rule = await new GstSerialNumberRules(parameters).RuleForAsync("2(b)", new FinancialYear(2027), TestContext.Current.CancellationToken);

        Assert.Equal(new SerialNumberRule(11, "-/"), rule);
        Assert.Equal(new DateOnly(2027, 4, 1), parameters.AskedFor);
    }

    [Fact]
    public async Task RuleFor_AClauseTheSetDoesNotHold_IsNull()
    {
        Assert.Null(await new GstSerialNumberRules(new AnsweringParameters(Set)).RuleForAsync("3(c)", new FinancialYear(2026), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RuleFor_AYearBeforeTheSetsFirstVersion_IsNull()
    {
        Assert.Null(await new GstSerialNumberRules(new AnsweringParameters(null)).RuleForAsync("1(a)", new FinancialYear(2025), TestContext.Current.CancellationToken));
    }

    private sealed class AnsweringParameters(GstDocumentSerialNumbers? set) : IStatutoryParameters
    {
        public DateOnly? AskedFor { get; private set; }

        public Task<TSet?> GetAsOfAsync<TSet>(DateOnly date, CancellationToken cancellationToken)
            where TSet : class, IStatutoryParameterSet<TSet>
        {
            AskedFor = date;

            return Task.FromResult(set as TSet);
        }
    }
}
