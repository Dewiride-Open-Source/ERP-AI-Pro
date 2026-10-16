using Dewiride.Erp.BuildingBlocks.Endpoints.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Security;

public sealed class FetchMetadataTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("same-origin", true)]
    [InlineData("same-site", false)]
    [InlineData("cross-site", false)]
    [InlineData("none", false)]
    [InlineData("Same-Origin", false)]
    [InlineData("", false)]
    public void IsFromThisOrigin_SiteTheBrowserNames_PassesOnlyWithoutOneOrForThisOrigin(string? site, bool passes)
    {
        Assert.Equal(passes, FetchMetadata.IsFromThisOrigin(Headers(("Sec-Fetch-Site", site))));
    }

    [Fact]
    public void IsFromThisOrigin_SiteSentTwice_IsRefused()
    {
        var headers = new HeaderDictionary { ["Sec-Fetch-Site"] = new StringValues(["same-origin", "same-origin"]) };

        Assert.False(FetchMetadata.IsFromThisOrigin(headers));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("navigate", "document", true)]
    [InlineData("navigate", null, true)]
    [InlineData(null, "document", true)]
    [InlineData("navigate", "iframe", false)]
    [InlineData("no-cors", "image", false)]
    [InlineData("cors", "empty", false)]
    public void IsOpenedAsAPage_ModeAndDestination_PassOnlyForAPageTheBrowserOpensItself(string? mode, string? destination, bool passes)
    {
        Assert.Equal(passes, FetchMetadata.IsOpenedAsAPage(Headers(("Sec-Fetch-Mode", mode), ("Sec-Fetch-Dest", destination))));
    }

    private static HeaderDictionary Headers(params (string Name, string? Value)[] values)
    {
        var headers = new HeaderDictionary();
        foreach (var (name, value) in values)
        {
            if (value is not null)
            {
                headers[name] = value;
            }
        }

        return headers;
    }
}
