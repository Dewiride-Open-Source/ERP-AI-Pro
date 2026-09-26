using System.Collections;

namespace Dewiride.Erp.Testing.Telemetry;

// In-memory exporters add from whatever thread ends a span or collects metrics, including work other test classes run in
// parallel, so reads take a snapshot under the same lock.
public sealed class ExportedItemCollection<T> : ICollection<T>
{
    private readonly List<T> _items = [];

    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    public bool IsReadOnly => false;

    public void Add(T item)
    {
        lock (_gate)
        {
            _items.Add(item);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
        }
    }

    public bool Contains(T item)
    {
        lock (_gate)
        {
            return _items.Contains(item);
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        lock (_gate)
        {
            _items.CopyTo(array, arrayIndex);
        }
    }

    public bool Remove(T item)
    {
        lock (_gate)
        {
            return _items.Remove(item);
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        lock (_gate)
        {
            return _items.ToList().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
