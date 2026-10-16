using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Changes;

public static class ChangeHistoryErrors
{
    public static readonly Error ExportTooLarge = Error.Failure(
        "audit.export-too-large",
        string.Create(CultureInfo.InvariantCulture, $"The export would hold more than {ChangeExport.MaxLines:N0} lines; narrow the filter, for example to fewer days."));
}
