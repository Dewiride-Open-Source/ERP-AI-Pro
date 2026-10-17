using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Origins;

// Work outside a request, such as a hosted service of the API, has no HttpContext and comes from the system.
internal sealed class HttpRequestOrigin(IHttpContextAccessor accessor) : IRequestOrigin
{
    private readonly SystemRequestOrigin _system = new();

    public string? ClientAddress => accessor.HttpContext is { } context ? RequestOrigins.ClientAddressOf(context) : _system.ClientAddress;

    public string? CorrelationId => accessor.HttpContext is { } context ? RequestOrigins.CorrelationIdOf(context) : _system.CorrelationId;
}
