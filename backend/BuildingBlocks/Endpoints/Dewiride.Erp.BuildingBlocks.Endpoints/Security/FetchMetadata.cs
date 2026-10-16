using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Security;

// A browser marks every request with the fetch metadata headers, and only a browser can be made to send a request by another
// site. A client that is no browser, such as the web app's server, sends none of them, so an absent header passes and a present
// one must hold exactly the one value a page of this site sends.
public static class FetchMetadata
{
    public const string SiteHeader = "Sec-Fetch-Site";

    public const string ModeHeader = "Sec-Fetch-Mode";

    public const string DestinationHeader = "Sec-Fetch-Dest";

    public static bool IsFromThisOrigin(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        return IsAbsentOrExactly(headers[SiteHeader], "same-origin");
    }

    public static bool IsOpenedAsAPage(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        return IsAbsentOrExactly(headers[ModeHeader], "navigate") && IsAbsentOrExactly(headers[DestinationHeader], "document");
    }

    private static bool IsAbsentOrExactly(StringValues values, string expected) =>
        values.Count == 0 || (values.Count == 1 && string.Equals(values[0], expected, StringComparison.Ordinal));
}
