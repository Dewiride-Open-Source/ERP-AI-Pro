using System.Diagnostics;
using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Dewiride.Erp.BuildingBlocks.Endpoints;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Origins;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Origins;

public sealed class HttpRequestOriginTests
{
    [Fact]
    public void Origin_InsideARequest_IsTheRequestsAddressAndCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:198.51.100.23");
        context.Features.Set<ICorrelationIdFeature>(new CorrelationIdFeature("0af7651916cd43dd8448eb211c80319c"));

        var origin = new HttpRequestOrigin(new HttpContextAccessor { HttpContext = context });

        Assert.Equal("198.51.100.23", origin.ClientAddress);
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", origin.CorrelationId);
    }

    [Fact]
    public void Origin_OutsideARequest_IsTheSystemsOrigin()
    {
        using var activity = new Activity("hosted-service").SetIdFormat(ActivityIdFormat.W3C).Start();

        var origin = new HttpRequestOrigin(new HttpContextAccessor { HttpContext = null });

        Assert.Null(origin.ClientAddress);
        Assert.Equal(activity.TraceId.ToHexString(), origin.CorrelationId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddErpEndpoints_WhicheverRegistrationRunsFirst_ResolvesTheRequestOrigin(bool persistenceFirst)
    {
        var builder = WebApplication.CreateBuilder();
        if (persistenceFirst)
        {
            builder.Services.AddErpPersistenceCore();
            builder.AddErpEndpoints();
        }
        else
        {
            builder.AddErpEndpoints();
            builder.Services.AddErpPersistenceCore();
        }

        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<HttpRequestOrigin>(scope.ServiceProvider.GetRequiredService<IRequestOrigin>());
        Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(IRequestOrigin));
    }
}
