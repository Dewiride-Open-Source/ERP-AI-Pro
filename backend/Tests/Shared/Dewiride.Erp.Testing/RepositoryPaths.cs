namespace Dewiride.Erp.Testing;

public static class RepositoryPaths
{
    private const string BackendSolution = "backend/Dewiride.Erp.slnx";

    public static string Root { get; } = Locate();

    public static string Combine(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        return Path.Combine([Root, .. segments]);
    }

    private static string Locate()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, BackendSolution.Replace('/', Path.DirectorySeparatorChar))))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No ancestor of '{AppContext.BaseDirectory}' holds '{BackendSolution}', so the repository root cannot be found.");
    }
}
