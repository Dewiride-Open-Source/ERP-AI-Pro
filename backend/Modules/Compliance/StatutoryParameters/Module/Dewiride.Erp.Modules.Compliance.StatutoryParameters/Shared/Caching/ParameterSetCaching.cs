namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Shared.Caching;

// A set changes only when a release seeds it again, and the version in force changes at midnight in India, so an answer kept
// for an hour is never staler than that. Only the Finance role and administrators may read the sets, so the browser reuses an
// answer only for the sign-in that read it and asks the API again for anyone else on the same browser.
internal static class ParameterSetCaching
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);
}
