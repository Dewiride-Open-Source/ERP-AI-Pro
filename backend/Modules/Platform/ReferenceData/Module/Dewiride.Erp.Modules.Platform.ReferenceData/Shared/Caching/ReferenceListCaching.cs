namespace Dewiride.Erp.Modules.Platform.ReferenceData.Shared.Caching;

// A list changes only when a release seeds it again, and the GST State codes in force change at midnight in India, so a
// response kept for an hour is never staler than that.
internal static class ReferenceListCaching
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);
}
