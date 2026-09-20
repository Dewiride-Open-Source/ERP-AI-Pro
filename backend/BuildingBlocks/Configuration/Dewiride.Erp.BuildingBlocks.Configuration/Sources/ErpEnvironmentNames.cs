namespace Dewiride.Erp.BuildingBlocks.Configuration.Sources;

public static class ErpEnvironmentNames
{
    public const string VariableName = "ERP_ENVIRONMENT";

    public const string LocalDev = "local-dev";

    public const string Production = "production";

    public static bool IsKnown(string? value) => value is LocalDev or Production;
}
