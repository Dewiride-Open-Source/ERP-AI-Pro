using Dewiride.Erp.Modules.Identity.Users.People.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Domain;

// Who reports to whom on any date, read from the person, start date and manager of every position. The chain of managers
// changes only on a date some position starts, and a change to one person's positions can only close a cycle through that
// person, so a change is checked on the first date it holds and on every later date a position starts while it holds. A walk
// stops at a manager it has seen already, so rows written outside the API that loop end the walk.
internal sealed class ReportingLines
{
    private readonly Dictionary<UserId, ReportingLine[]> _byPerson;

    private readonly DateOnly[] _startDates;

    public ReportingLines(IEnumerable<ReportingLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var all = lines.ToList();
        _byPerson = all.GroupBy(line => line.Person).ToDictionary(group => group.Key, group => group.OrderBy(line => line.EffectiveFrom).ToArray());
        _startDates = [.. all.Select(line => line.EffectiveFrom).Distinct().Order()];
    }

    public UserId? ManagerOf(UserId person, DateOnly on)
    {
        if (!_byPerson.TryGetValue(person, out var lines))
        {
            return null;
        }

        UserId? manager = null;
        foreach (var line in lines)
        {
            if (line.EffectiveFrom > on)
            {
                break;
            }

            manager = line.Manager;
        }

        return manager;
    }

    public bool ClosesCycle(UserId person, DateOnly from, DateOnly? until)
    {
        var dates = _startDates.Where(date => date > from && (until is not { } end || date < end)).Prepend(from);

        return dates.Any(date => ReportsToThemself(person, date));
    }

    private bool ReportsToThemself(UserId person, DateOnly on)
    {
        var seen = new HashSet<UserId>();
        for (var manager = ManagerOf(person, on); manager is { } id && seen.Add(id); manager = ManagerOf(id, on))
        {
            if (id == person)
            {
                return true;
            }
        }

        return false;
    }
}
