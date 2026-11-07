namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;

public sealed record SacCode(string Code, SacCodeLevel Level, string? Parent, string Description);
