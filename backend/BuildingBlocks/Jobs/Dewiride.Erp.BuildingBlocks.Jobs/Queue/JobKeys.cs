using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Queue;

// A key is built from ids and dates, never from names, and its characters leave out spaces and @, so no email address fits one.
// Keys compare without regard to case, as the database's collation compares them. The scheduler's keys begin with the reserved
// prefix, so no enqueue can take or pre-empt an occurrence.
internal static partial class JobKeys
{
    public const int MaxLength = 200;

    public const string ReservedPrefix = OccurrenceKeys.Prefix;

    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public static bool IsWellFormed(string? key) => key is not null && Pattern().IsMatch(key);

    public static void EnsureEnqueueable(string? idempotencyKey)
    {
        if (!IsWellFormed(idempotencyKey))
        {
            throw new ArgumentException($"An idempotency key is 1 to {MaxLength} letters, digits and the characters . _ : + -, built from ids and dates.", nameof(idempotencyKey));
        }

        if (idempotencyKey!.StartsWith(ReservedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Idempotency keys that begin with '{ReservedPrefix}' belong to the scheduler.", nameof(idempotencyKey));
        }
    }

    // \z rather than $, which would also match before a trailing line feed.
    [GeneratedRegex(@"^[A-Za-z0-9._:+\-]{1,200}\z")]
    private static partial Regex Pattern();
}
