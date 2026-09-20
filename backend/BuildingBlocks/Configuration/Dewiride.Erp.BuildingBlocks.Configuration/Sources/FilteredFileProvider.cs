using System.Collections;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Dewiride.Erp.BuildingBlocks.Configuration.Sources;

internal sealed class FilteredFileProvider(IFileProvider inner, Func<string, bool> isHidden) : IFileProvider
{
    public IDirectoryContents GetDirectoryContents(string subpath) =>
        new FilteredDirectoryContents(inner.GetDirectoryContents(subpath), isHidden);

    public IFileInfo GetFileInfo(string subpath)
    {
        var file = inner.GetFileInfo(subpath);

        return !file.IsDirectory && isHidden(file.Name) ? new NotFoundFileInfo(subpath) : file;
    }

    public IChangeToken Watch(string filter) => inner.Watch(filter);

    private sealed class FilteredDirectoryContents(IDirectoryContents inner, Func<string, bool> isHidden) : IDirectoryContents
    {
        public bool Exists => inner.Exists;

        public IEnumerator<IFileInfo> GetEnumerator() =>
            inner.Where(file => file.IsDirectory || !isHidden(file.Name)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
