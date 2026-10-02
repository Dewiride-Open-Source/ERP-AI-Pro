using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

// Copies go in and out, so no caller can change a stored key element after the fact.
internal sealed class InMemoryKeyRepository : IXmlRepository
{
    private readonly Lock _gate = new();

    private readonly List<XElement> _elements = [];

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_gate)
        {
            return _elements.Select(element => new XElement(element)).ToList();
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);

        var copy = new XElement(element);
        lock (_gate)
        {
            _elements.Add(copy);
        }
    }
}
