using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Kernel.Events;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;

internal static partial class IntegrationEventNames
{
    public const int MaxLength = 128;

    public static bool IsWellFormed(string? name) => name is not null && name.Length <= MaxLength && Pattern().IsMatch(name);

    public static string Of<TEvent>()
        where TEvent : IIntegrationEvent
    {
        var name = TEvent.Name;
        if (!IsWellFormed(name))
        {
            throw new InvalidOperationException($"{typeof(TEvent).Name} is named '{name}'; an integration event's name is three lower-case kebab segments, <domain>.<module>.<event>, of at most {MaxLength} characters.");
        }

        return name;
    }

    // \z rather than $, which would also match before a trailing line feed.
    [GeneratedRegex(@"^[a-z][a-z0-9]*(-[a-z0-9]+)*(\.[a-z][a-z0-9]*(-[a-z0-9]+)*){2}\z")]
    private static partial Regex Pattern();
}
