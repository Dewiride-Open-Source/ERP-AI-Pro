using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.RateLimiting;

public sealed class ClientAddressPartitionTests
{
    [Fact]
    public void For_NoAddress_ReturnsTheUnknownPartition()
    {
        Assert.Equal(ClientAddressPartition.Unknown, ClientAddressPartition.For(null));
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("::1", "::/64")]
    public void For_Address_ReturnsItsPartition(string address, string partition)
    {
        Assert.Equal(partition, ClientAddressPartition.For(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2:ffff:ffff:ffff:ffff", true)]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:3::1", false)]
    public void For_TwoIpv6Addresses_SharesAPartitionOnlyWithinOneSlash64(string first, string second, bool shared)
    {
        Assert.Equal(shared, ClientAddressPartition.For(IPAddress.Parse(first)) == ClientAddressPartition.For(IPAddress.Parse(second)));
    }
}
