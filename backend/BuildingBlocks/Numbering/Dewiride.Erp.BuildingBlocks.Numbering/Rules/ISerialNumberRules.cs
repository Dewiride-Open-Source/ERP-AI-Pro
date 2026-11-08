using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Rules;

// The law that limits a kind of document's numbers is a statutory parameter, so a module that reads the parameter store answers
// it; null means the ERP holds no rule of that key for that financial year.
public interface ISerialNumberRules
{
    Task<SerialNumberRule?> RuleForAsync(string rule, FinancialYear year, CancellationToken cancellationToken);
}
