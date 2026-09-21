namespace Dewiride.Erp.ArchitectureTests;

internal static class RepositoryLayout
{
    public const string SolutionFile = "Dewiride.Erp.slnx";

    public static readonly string[] SourceRoots = ["BuildingBlocks", "Hosts", "Modules", "Tests"];

    private static readonly string[] GeneratedSegments = ["bin", "obj", "Migrations"];

    public static string BackendRoot { get; } = Locate();

    public static IEnumerable<string> SourceFiles() =>
        SourceRoots
            .Select(root => Path.Combine(BackendRoot, root))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(file => !IsGenerated(file));

    public static string? ProjectDirectory(string file)
    {
        for (var directory = Path.GetDirectoryName(file); directory is not null && directory.StartsWith(BackendRoot, StringComparison.OrdinalIgnoreCase); directory = Path.GetDirectoryName(directory))
        {
            if (Directory.EnumerateFiles(directory, "*.csproj").Any())
            {
                return directory;
            }
        }

        return null;
    }

    public static string Relative(string path) => Path.GetRelativePath(BackendRoot, path).Replace('\\', '/');

    private static bool IsGenerated(string file) =>
        Relative(file).Split('/').Any(segment => GeneratedSegments.Contains(segment, StringComparer.Ordinal));

    private static string Locate()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"{SolutionFile} was not found above {AppContext.BaseDirectory}.");
    }
}
