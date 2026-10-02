namespace Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

public sealed class KeyRingOptions
{
    public const string SectionName = "Erp:Platform:DataProtection";

    public const string KeyIdentifierKey = $"{SectionName}:{nameof(KeyIdentifier)}";

    public Uri? KeyIdentifier { get; set; }
}
