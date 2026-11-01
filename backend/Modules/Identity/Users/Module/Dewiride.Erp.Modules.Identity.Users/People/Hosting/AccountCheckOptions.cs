namespace Dewiride.Erp.Modules.Identity.Users.People.Hosting;

// Bound without validation: the scheduler reads the schedule at every poll, so a refreshed value applies without a restart, and
// a missing or invalid one stops only the nightly check, which the jobs page shows, never the API's start. The schedule has no
// default here, because App Configuration holds it.
internal sealed class AccountCheckOptions
{
    public const string SectionName = "Erp:Identity:Users";

    public string AccountCheckSchedule { get; set; } = string.Empty;
}
