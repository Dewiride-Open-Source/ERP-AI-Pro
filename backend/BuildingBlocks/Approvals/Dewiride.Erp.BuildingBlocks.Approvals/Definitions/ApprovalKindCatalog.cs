namespace Dewiride.Erp.BuildingBlocks.Approvals.Definitions;

// The kinds this release asks approval for. A request of a kind no release declares stays in its table, so its history stays
// readable under its key, and can be neither decided nor withdrawn on the Approvals page.
internal sealed class ApprovalKindCatalog
{
    private readonly Dictionary<string, ApprovalKindRegistration> _byKey = new(StringComparer.Ordinal);

    public ApprovalKindCatalog(IEnumerable<ApprovalKindRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        foreach (var registration in registrations)
        {
            registration.Kind.EnsureValid();
            if (!_byKey.TryAdd(registration.Kind.Key, registration))
            {
                throw new InvalidOperationException($"The approval kind {registration.Kind.Key} is declared more than once.");
            }
        }

        Kinds = [.. _byKey.Values.OrderBy(registration => registration.Kind.Title, StringComparer.Ordinal).ThenBy(registration => registration.Kind.Key, StringComparer.Ordinal)];
    }

    public IReadOnlyList<ApprovalKindRegistration> Kinds { get; }

    public ApprovalKindRegistration? Find(string key) => _byKey.GetValueOrDefault(key);

    public string TitleOf(string key) => Find(key)?.Kind.Title ?? key;
}
