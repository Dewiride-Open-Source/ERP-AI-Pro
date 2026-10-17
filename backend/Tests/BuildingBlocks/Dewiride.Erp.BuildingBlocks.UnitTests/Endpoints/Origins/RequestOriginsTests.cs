using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Origins;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Origins;

public sealed class RequestOriginsTests
{
    [Theory]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::7", "2001:db8::7")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    public void ClientAddressOf_Request_IsTheAddressInTheFamilyItWasSentIn(string address, string recorded)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        Assert.Equal(recorded, RequestOrigins.ClientAddressOf(context));
    }

    [Fact]
    public void ClientAddressOf_RequestWithoutAnAddress_IsNone()
    {
        Assert.Null(RequestOrigins.ClientAddressOf(new DefaultHttpContext()));
    }

    [Fact]
    public void CorrelationIdOf_RequestWithACorrelationId_IsThatId()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "0HN7:00000001" };
        context.Features.Set<ICorrelationIdFeature>(new CorrelationIdFeature("4bf92f3577b34da6a3ce929d0e0e4736"));

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", RequestOrigins.CorrelationIdOf(context));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void CorrelationIdOf_RequestWithoutACorrelationId_IsItsTraceIdentifierWhenItFits(int length, bool kept)
    {
        var context = new DefaultHttpContext { TraceIdentifier = new string('a', length) };

        Assert.Equal(kept ? context.TraceIdentifier : null, RequestOrigins.CorrelationIdOf(context));
    }
}
