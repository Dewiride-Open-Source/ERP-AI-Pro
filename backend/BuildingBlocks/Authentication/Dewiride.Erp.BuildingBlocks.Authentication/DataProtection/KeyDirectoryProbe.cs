namespace Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

// Writes and deletes a file the way FileSystemXmlRepository writes a key: to a .tmp file in the key folder, which outside
// Windows the repository first creates with Path.GetTempFileName, for its owner-only mode, and moves into the folder, so there
// the temporary folder must be writable too. The repository reads only *.xml, so a probe left behind by a crash is never
// taken for a key.
internal sealed class KeyDirectoryProbe(bool stagesThroughTemporaryFolder, Func<string> createTemporaryFile)
{
    public const string ProbeExtension = ".tmp";

    public static KeyDirectoryProbe ForThisSystem { get; } = new(!OperatingSystem.IsWindows(), Path.GetTempFileName);

    public bool StagesThroughTemporaryFolder { get; } = stagesThroughTemporaryFolder;

    public static string UnwritableDirectoryMessage(string directory) => $"The Data Protection key folder {directory} cannot be written.";

    public static string UnwritableTemporaryFolderMessage(string temporaryFolder) =>
        $"The temporary folder {temporaryFolder}, through which Data Protection writes each key into its key folder, cannot be written.";

    public async Task ProbeAsync(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        var probe = Path.Combine(directory.FullName, Guid.CreateVersion7().ToString("N") + ProbeExtension);
        var staged = StagesThroughTemporaryFolder ? CreateStagedFile() : null;
        try
        {
            directory.Create();
            if (staged is not null)
            {
                File.Move(staged, probe);
                staged = null;
            }

            await File.WriteAllTextAsync(probe, KeyRingStartupCheck.ProbePurpose, cancellationToken).ConfigureAwait(false);
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException(UnwritableDirectoryMessage(directory.FullName), exception);
        }
        finally
        {
            if (staged is not null)
            {
                File.Delete(staged);
            }
        }
    }

    private string CreateStagedFile()
    {
        try
        {
            return createTemporaryFile();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException(UnwritableTemporaryFolderMessage(Path.GetTempPath()), exception);
        }
    }
}
