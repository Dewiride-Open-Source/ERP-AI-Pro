using System.Text.RegularExpressions;

namespace Dewiride.Erp.ArchitectureTests.Rules;

public sealed partial class SourceLayoutTests
{
    private const int MaxSourceFilesPerFolder = 12;

    private static readonly string[] ModuleProjectFolders = ["Contracts", "Module", "Tests/UnitTests", "Tests/IntegrationTests"];

    [Fact]
    public void EveryFile_DeclaresTheNamespaceOfItsFolder()
    {
        var violations = new List<string>();

        foreach (var file in RepositoryLayout.SourceFiles())
        {
            var match = NamespaceDeclaration().Match(File.ReadAllText(file));
            if (!match.Success)
            {
                continue;
            }

            var project = RepositoryLayout.ProjectDirectory(file);
            if (project is null)
            {
                violations.Add($"{RepositoryLayout.Relative(file)} is not inside a project");
                continue;
            }

            var folders = Path.GetRelativePath(project, Path.GetDirectoryName(file)!).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Where(segment => segment != ".");
            var expected = string.Join('.', new[] { Path.GetFileName(project) }.Concat(folders));
            if (!string.Equals(match.Groups["name"].Value, expected, StringComparison.Ordinal))
            {
                violations.Add($"{RepositoryLayout.Relative(file)} declares '{match.Groups["name"].Value}' instead of '{expected}'");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryModule_HasTheFourProjectsAndAReadme()
    {
        var violations = new List<string>();

        foreach (var domain in Directory.EnumerateDirectories(Path.Combine(RepositoryLayout.BackendRoot, "Modules")))
        {
            foreach (var module in Directory.EnumerateDirectories(domain))
            {
                var assembly = $"{ErpAssemblies.ModulesPrefix}{Path.GetFileName(domain)}.{Path.GetFileName(module)}";
                if (!File.Exists(Path.Combine(module, "README.md")))
                {
                    violations.Add($"{RepositoryLayout.Relative(module)} has no README.md");
                }

                foreach (var folder in ModuleProjectFolders)
                {
                    var project = folder switch
                    {
                        "Contracts" => assembly + ErpAssemblies.ContractsSuffix,
                        "Module" => assembly,
                        _ => assembly + "." + Path.GetFileName(folder),
                    };
                    if (!File.Exists(Path.Combine(module, folder, project, project + ".csproj")))
                    {
                        violations.Add($"{RepositoryLayout.Relative(module)} lacks {folder}/{project}/{project}.csproj");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryFolder_HoldsAtMostTwelveSourceFiles()
    {
        var offenders = RepositoryLayout.SourceFiles()
            .GroupBy(file => Path.GetDirectoryName(file)!, StringComparer.OrdinalIgnoreCase)
            .Where(folder => folder.Count() > MaxSourceFilesPerFolder)
            .Select(folder => $"{RepositoryLayout.Relative(folder.Key)} holds {folder.Count()} source files")
            .ToList();

        Assert.Empty(offenders);
    }

    [GeneratedRegex(@"^namespace\s+(?<name>[\w.]+)\s*[;{]", RegexOptions.Multiline)]
    private static partial Regex NamespaceDeclaration();
}
