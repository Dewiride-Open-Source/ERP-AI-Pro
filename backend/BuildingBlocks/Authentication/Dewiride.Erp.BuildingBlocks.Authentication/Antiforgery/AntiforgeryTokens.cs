namespace Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;

// The web app reads the request token from RequestTokenCookieName and sends it back in HeaderName; FormFieldName carries it
// only on an endpoint that reads a form, such as the sign-out form a page posts.
public static class AntiforgeryTokens
{
    public const string HeaderName = "X-XSRF-TOKEN";

    public const string FormFieldName = "__RequestVerificationToken";

    public const string CookieName = "__Host-erp-antiforgery";

    public const string RequestTokenCookieName = "__Host-erp-xsrf";
}
