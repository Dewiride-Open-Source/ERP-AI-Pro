using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;

internal static class ParameterSetErrors
{
    public static readonly Error NotFound = Error.NotFound("parameter-set.not-found", "No statutory parameter set has this key.");
}
