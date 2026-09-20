namespace Dewiride.Erp.BuildingBlocks.Modules.Features;

/// <summary>Evaluated state of one feature flag.</summary>
/// <param name="Name">Flag name, <c>Erp.Modules.&lt;Domain&gt;.&lt;Module&gt;</c> or <c>Erp.Modules.&lt;Domain&gt;.&lt;Module&gt;.&lt;Capability&gt;</c>.</param>
/// <param name="Enabled">Whether the flag evaluates as enabled for this request.</param>
internal sealed record FeatureStateResponse(string Name, bool Enabled);
