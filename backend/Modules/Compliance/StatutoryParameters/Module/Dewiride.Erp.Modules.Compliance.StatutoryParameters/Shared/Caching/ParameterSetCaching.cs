namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Shared.Caching;

// A set changes only when a release seeds it again, and the version in force changes at midnight in India, so an answer kept
// for an hour is never staler than that.
internal static class ParameterSetCaching
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);
}
