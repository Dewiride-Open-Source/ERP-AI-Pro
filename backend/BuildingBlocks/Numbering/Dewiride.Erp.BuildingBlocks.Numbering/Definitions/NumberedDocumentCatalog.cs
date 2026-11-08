namespace Dewiride.Erp.BuildingBlocks.Numbering.Definitions;

// The kinds of document this release numbers. A numbering row of a key no release declares stays in the table, so its numbers
// keep their history, and is neither listed nor used.
internal sealed class NumberedDocumentCatalog
{
    private readonly Dictionary<string, NumberedDocument> _byKey = new(StringComparer.Ordinal);

    public NumberedDocumentCatalog(IEnumerable<NumberedDocumentRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        foreach (var registration in registrations)
        {
            registration.Document.EnsureValid();
            if (!_byKey.TryAdd(registration.Document.Key, registration.Document))
            {
                throw new InvalidOperationException($"The numbered document {registration.Document.Key} is declared more than once.");
            }
        }

        Documents = [.. _byKey.Values.OrderBy(document => document.Title, StringComparer.Ordinal).ThenBy(document => document.Key, StringComparer.Ordinal)];
    }

    public IReadOnlyList<NumberedDocument> Documents { get; }

    public NumberedDocument? Find(string key) => _byKey.GetValueOrDefault(key);
}
