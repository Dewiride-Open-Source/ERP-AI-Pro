using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.DataProtection;

// The probe stages through Path.GetTempFileName outside Windows, and the temporary folder belongs to the whole test process,
// so these tests hand the probe a staging function of their own instead of making that folder unwritable.
public sealed class KeyDirectoryProbeTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("erp-key-directory-probe-");

    [Fact]
    public void ForThisSystem_StagingRule_IsTheRepositorysOutsideWindows()
    {
        Assert.Equal(!OperatingSystem.IsWindows(), KeyDirectoryProbe.ForThisSystem.StagesThroughTemporaryFolder);
    }

    [Fact]
    public async Task ProbeAsync_OnThisSystem_CreatesTheKeyFolderAndLeavesNothingInIt()
    {
        var keys = KeyFolder();

        await KeyDirectoryProbe.ForThisSystem.ProbeAsync(keys, TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(keys.FullName));
        Assert.Empty(Directory.EnumerateFileSystemEntries(keys.FullName));
    }

    [Fact]
    public async Task ProbeAsync_DirectlyInTheKeyFolder_NeverStagesAFile()
    {
        var keys = KeyFolder();
        var probe = new KeyDirectoryProbe(stagesThroughTemporaryFolder: false, () => throw new InvalidOperationException("The probe staged a file."));

        await probe.ProbeAsync(keys, TestContext.Current.CancellationToken);

        Assert.Empty(Directory.EnumerateFileSystemEntries(keys.FullName));
    }

    [Fact]
    public async Task ProbeAsync_ThroughTheTemporaryFolder_MovesTheStagedFileIntoTheKeyFolderAndLeavesNothingBehind()
    {
        var staging = _root.CreateSubdirectory("staging");
        var keys = KeyFolder();
        var staged = new List<string>();
        var probe = new KeyDirectoryProbe(stagesThroughTemporaryFolder: true, () => Stage(staging, staged));

        await probe.ProbeAsync(keys, TestContext.Current.CancellationToken);

        Assert.Single(staged);
        Assert.Empty(Directory.EnumerateFileSystemEntries(staging.FullName));
        Assert.Empty(Directory.EnumerateFileSystemEntries(keys.FullName));
    }

    [Fact]
    public async Task ProbeAsync_WhenTheTemporaryFolderCannotBeWritten_FailsNamingItBeforeTouchingTheKeyFolder()
    {
        var keys = KeyFolder();
        var cause = new IOException("Read-only file system.");
        var probe = new KeyDirectoryProbe(stagesThroughTemporaryFolder: true, () => throw cause);

        var failure = await Assert.ThrowsAsync<IOException>(() => probe.ProbeAsync(keys, TestContext.Current.CancellationToken));

        Assert.Equal(KeyDirectoryProbe.UnwritableTemporaryFolderMessage(Path.GetTempPath()), failure.Message);
        Assert.Same(cause, failure.InnerException);
        Assert.False(Directory.Exists(keys.FullName));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProbeAsync_WhenAFileStandsWhereTheKeyFolderShouldBe_FailsNamingTheFolderAndLeavesNoStagedFile(bool stagesThroughTemporaryFolder)
    {
        var staging = _root.CreateSubdirectory("staging");
        var keys = KeyFolder();
        await File.WriteAllTextAsync(keys.FullName, "a file where the key folder should be", TestContext.Current.CancellationToken);
        var probe = new KeyDirectoryProbe(stagesThroughTemporaryFolder, () => Stage(staging, []));

        var failure = await Assert.ThrowsAsync<IOException>(() => probe.ProbeAsync(keys, TestContext.Current.CancellationToken));

        Assert.Equal(KeyDirectoryProbe.UnwritableDirectoryMessage(keys.FullName), failure.Message);
        Assert.NotNull(failure.InnerException);
        Assert.Empty(Directory.EnumerateFileSystemEntries(staging.FullName));
    }

    public void Dispose()
    {
        _root.Delete(recursive: true);
    }

    private static string Stage(DirectoryInfo staging, List<string> staged)
    {
        var path = Path.Combine(staging.FullName, Guid.CreateVersion7().ToString("N") + KeyDirectoryProbe.ProbeExtension);
        File.WriteAllBytes(path, []);
        staged.Add(path);

        return path;
    }

    private DirectoryInfo KeyFolder() => new(Path.Combine(_root.FullName, "keys"));
}
