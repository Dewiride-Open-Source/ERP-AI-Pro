namespace Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

// The jobs this release defines. A runner claims only runs of these names, so a run of a job that another release defines stays
// for the instance that knows it, and the retention sweep keeps the schedule rows of these recurring jobs alone.
internal sealed class JobCatalog
{
    private readonly Dictionary<string, JobRegistration> _byName = new(StringComparer.Ordinal);

    private readonly Dictionary<Type, JobRegistration> _byType = [];

    public JobCatalog(IEnumerable<JobRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        foreach (var registration in registrations)
        {
            if (!_byName.TryAdd(registration.Name, registration))
            {
                throw new InvalidOperationException($"The job {registration.Name} is registered more than once.");
            }

            if (!_byType.TryAdd(registration.JobType, registration))
            {
                throw new InvalidOperationException($"{registration.JobType.Name} is registered as both {_byType[registration.JobType].Name} and {registration.Name}.");
            }
        }

        Jobs = [.. _byName.Values.OrderBy(registration => registration.Name, StringComparer.Ordinal)];
        Names = [.. Jobs.Select(registration => registration.Name)];
        Recurring = [.. Jobs.Where(registration => registration.IsRecurring)];
    }

    public IReadOnlyList<JobRegistration> Jobs { get; }

    public IReadOnlyList<string> Names { get; }

    public IReadOnlyList<JobRegistration> Recurring { get; }

    public JobRegistration? Find(string name) => _byName.GetValueOrDefault(name);

    public JobRegistration? Find(Type jobType) => _byType.GetValueOrDefault(jobType);

    public JobRegistration Require(Type jobType, Type? payloadType)
    {
        ArgumentNullException.ThrowIfNull(jobType);

        var registration = Find(jobType)
            ?? throw new InvalidOperationException($"{jobType.Name} is not a registered job; register it with AddJob or AddRecurringJob in its module's services.");
        if (registration.PayloadType != payloadType)
        {
            var expected = registration.PayloadType is null ? "no payload" : $"a payload of {registration.PayloadType.Name}";
            throw new InvalidOperationException($"The job {registration.Name} takes {expected}.");
        }

        return registration;
    }
}
