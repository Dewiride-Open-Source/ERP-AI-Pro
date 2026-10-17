using Dewiride.Erp.BuildingBlocks.Application.Origins;

namespace Dewiride.Erp.Modules.Platform.Audit.UnitTests.Changes.Application;

internal sealed record TestRequestOrigin(string? ClientAddress, string? CorrelationId) : IRequestOrigin;
