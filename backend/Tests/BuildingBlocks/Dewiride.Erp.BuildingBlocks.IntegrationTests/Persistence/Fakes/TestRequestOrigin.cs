using Dewiride.Erp.BuildingBlocks.Application.Origins;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Fakes;

public sealed class TestRequestOrigin : IRequestOrigin
{
    public string? ClientAddress { get; set; } = "198.51.100.23";

    public string? CorrelationId { get; set; } = Guid.CreateVersion7().ToString("N");
}
