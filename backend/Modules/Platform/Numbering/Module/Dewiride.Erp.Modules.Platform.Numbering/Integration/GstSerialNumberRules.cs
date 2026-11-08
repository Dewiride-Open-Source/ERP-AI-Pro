using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Rules;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Documents;

namespace Dewiride.Erp.Modules.Platform.Numbering.Integration;

// A kind of document names the clause of the CGST Rules its numbers follow, which the parameter set gst.document-serial-numbers
// holds by clause; a year's numbering opens with its first number, so the set is read as of the year's first day.
internal sealed class GstSerialNumberRules(IStatutoryParameters parameters) : ISerialNumberRules
{
    public async Task<SerialNumberRule?> RuleForAsync(string rule, FinancialYear year, CancellationToken cancellationToken)
    {
        var set = await parameters.GetAsOfAsync<GstDocumentSerialNumbers>(year.Start, cancellationToken).ConfigureAwait(false);
        var clause = set?.Rules.SingleOrDefault(candidate => string.Equals(candidate.Rule, rule, StringComparison.Ordinal));

        return clause is null ? null : new SerialNumberRule(clause.MaxLength, clause.SpecialCharacters);
    }
}
