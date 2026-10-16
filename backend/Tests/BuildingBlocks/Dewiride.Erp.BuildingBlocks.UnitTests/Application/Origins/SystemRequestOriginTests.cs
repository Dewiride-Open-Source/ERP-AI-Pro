using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.Application.Origins;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.Origins;

public sealed class SystemRequestOriginTests
{
    [Fact]
    public void ClientAddress_IsNone()
    {
        Assert.Null(new SystemRequestOrigin().ClientAddress);
    }

    [Fact]
    public void CorrelationId_InsideATrace_IsItsTraceId()
    {
        using var activity = new Activity("system-work").SetIdFormat(ActivityIdFormat.W3C).Start();

        Assert.Equal(activity.TraceId.ToHexString(), new SystemRequestOrigin().CorrelationId);
    }

    [Fact]
    public void CorrelationId_InsideAHierarchicalActivity_IsNone()
    {
        using var activity = new Activity("legacy-work").SetIdFormat(ActivityIdFormat.Hierarchical).Start();

        Assert.Null(new SystemRequestOrigin().CorrelationId);
    }

    [Fact]
    public void CorrelationId_OutsideAnyTrace_IsNone()
    {
        var current = Activity.Current;
        Activity.Current = null;
        try
        {
            Assert.Null(new SystemRequestOrigin().CorrelationId);
        }
        finally
        {
            Activity.Current = current;
        }
    }
}
