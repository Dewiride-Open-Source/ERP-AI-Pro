namespace Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;

internal sealed class CorrelationIdFeature(string correlationId) : ICorrelationIdFeature
{
    public string CorrelationId => correlationId;
}
