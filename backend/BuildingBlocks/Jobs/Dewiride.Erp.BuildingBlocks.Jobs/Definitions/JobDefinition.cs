namespace Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

// The name identifies the job's runs, its schedule row and the scope of its idempotency keys, so it never follows a class name:
// renaming a job leaves its earlier runs under a name that no release defines.
public sealed record JobDefinition(string Name, string Title, string Description, JobRetryPolicy Retry, TimeSpan AttemptTimeout)
{
    public const int NameMaxLength = JobNames.MaxLength;

    public const int TitleMaxLength = 100;

    public const int DescriptionMaxLength = 400;

    public static readonly TimeSpan ShortestAttemptTimeout = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan LongestAttemptTimeout = TimeSpan.FromDays(1);

    internal void EnsureValid()
    {
        if (!JobNames.IsWellFormed(Name))
        {
            throw new ArgumentException($"The job name '{Name}' must be three lower-case kebab segments, <domain>.<module>.<job>, of at most {JobNames.MaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(Title) || Title.Length > TitleMaxLength)
        {
            throw new ArgumentException($"The job {Name} needs a title of 1 to {TitleMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(Description) || Description.Length > DescriptionMaxLength)
        {
            throw new ArgumentException($"The job {Name} needs a description of 1 to {DescriptionMaxLength} characters.");
        }

        ArgumentNullException.ThrowIfNull(Retry);
        Retry.EnsureValid(Name);
        if (AttemptTimeout < ShortestAttemptTimeout || AttemptTimeout > LongestAttemptTimeout)
        {
            throw new ArgumentException($"The job {Name} allows an attempt {AttemptTimeout}; an attempt is allowed {ShortestAttemptTimeout} to {LongestAttemptTimeout}.");
        }
    }
}
