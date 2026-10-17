namespace Dewiride.Erp.BuildingBlocks.Application.Origins;

public interface IRequestOrigin
{
    string? ClientAddress { get; }

    string? CorrelationId { get; }
}
