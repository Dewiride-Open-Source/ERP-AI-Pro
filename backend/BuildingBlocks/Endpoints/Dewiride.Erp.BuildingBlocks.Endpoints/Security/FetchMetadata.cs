using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Security;

// A browser marks its requests with the fetch metadata headers, and only a browser can be made to send a request by another
// site. A client that is no browser sends no Sec-Fetch-Site, so an absent header passes and a present one must hold exactly the
// one value a page of this site sends. Node's fetch, through which the web app's server calls, sends Sec-Fetch-Mode: cors, so
// IsOpenedAsAPage holds for a navigation the browser opens itself and never for a call of the web server.
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
