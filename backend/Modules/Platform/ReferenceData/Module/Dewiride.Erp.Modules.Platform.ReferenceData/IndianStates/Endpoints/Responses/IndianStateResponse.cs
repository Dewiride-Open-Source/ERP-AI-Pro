using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Endpoints.Responses;

/// <summary>One GST State code over the dates it stands for one State, Union Territory or the Other Territory.</summary>
/// <param name="GstCode">Two-digit GST State code, the first two digits of a GSTIN.</param>
/// <param name="Name">Name of the State or Union Territory as the Government of India spells it.</param>
/// <param name="Kind">Whether the code is a State, a Union Territory or the Other Territory.</param>
/// <param name="ValidFrom">First date the code stands for this territory.</param>
/// <param name="ValidUntil">Last date the code stands for this territory; null while it still does.</param>
internal sealed record IndianStateResponse(string GstCode, string Name, IndianStateKind Kind, DateOnly ValidFrom, DateOnly? ValidUntil);
