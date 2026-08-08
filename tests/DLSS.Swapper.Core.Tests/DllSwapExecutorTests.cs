using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Swapping;
using Xunit;

namespace DLSS_Swapper.Tests;

public class DllSwapExecutorTests
{
    // A game shipping the same dll in two places, which is common enough that the app has dedicated
    // "multiple dlls found" UI for it.
    const string TargetA = @"C:\Games\Example\Binaries\nvngx_dlss.dll";
    const string TargetB = @"C:\Games\Example\Engine\Binaries\nvngx_dlss.dll";
    const string BackupA = TargetA + DllSwapExecutor.BackupSuffix;
    const string BackupB = TargetB + DllSwapExecutor.BackupSuffix;

    const string DownloadedDll = @"C:\Users\Example\AppData\Local\dlss_swapper\dlls\dlss_3.8\nvngx_dlss.dll";

    static IReadOnlyList<string> BothTargets => new[] { TargetA, TargetB };

    #region Backups are decided per file, not per game

    /// <summary>
    /// Regression: the backup step used to be gated on whether <em>any</em> backup existed for the
    /// asset type. With two dll locations and a backup for only one of them, the second was
    /// overwritten with no backup and its original was gone for good.
    /// </summary>
    [Fact]
    public void Swap_WhenOnlyOneTargetHasABackup_StillBacksUpTheOther()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-3.5")            // already swapped once
            .AddFile(BackupA, "dlss-original-a")     // so it has a backup
            .AddFile(TargetB, "dlss-original-b")     // never swapped, no backup
            .AddFile(DownloadedDll, "dlss-3.8");

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, BothTargets);

        Assert.True(result.Success);
        Assert.True(fileSystem.FileExists(BackupB));
        Assert.Equal("dlss-original-b", fileSystem.ReadFile(BackupB));
        Assert.Equal(BackupB, Assert.Single(result.CreatedBackups).BackupPath);
    }

    /// <summary>
    /// A backup exists to get back to the game's original dll, so a later swap must never overwrite
    /// it with the dll from a previous swap.
    /// </summary>
    [Fact]
    public void Swap_WhenTargetAlreadyHasABackup_LeavesTheOriginalBackupIntact()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-3.5")
            .AddFile(BackupA, "dlss-original-a")
            .AddFile(DownloadedDll, "dlss-3.8");

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, new[] { TargetA });

        Assert.True(result.Success);
        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetA));
        Assert.Equal("dlss-original-a", fileSystem.ReadFile(BackupA));
        Assert.Empty(result.CreatedBackups);
    }

    /// <summary>
    /// The mixed-backup case end to end: after swapping, resetting has to return both locations to
    /// their own originals rather than only the one that happened to have a backup already.
    /// </summary>
    [Fact]
    public void SwapThenReset_WithMixedBackupState_ReturnsEveryTargetToItsOriginal()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-3.5")
            .AddFile(BackupA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8");

        var executor = new DllSwapExecutor(fileSystem);

        Assert.True(executor.Swap(DownloadedDll, BothTargets).Success);
        Assert.True(executor.Reset(BothTargets).Success);

        Assert.Equal("dlss-original-a", fileSystem.ReadFile(TargetA));
        Assert.Equal("dlss-original-b", fileSystem.ReadFile(TargetB));
        Assert.False(fileSystem.FileExists(BackupA));
        Assert.False(fileSystem.FileExists(BackupB));
    }

    #endregion

    #region A failed swap changes nothing

    /// <summary>
    /// Regression: the swap loop used to copy each target in turn and bail out on the first failure,
    /// leaving earlier targets swapped on disk while reporting failure and writing nothing to the
    /// database. A later rescan then read that as an external change and deleted the backup.
    /// </summary>
    [Fact]
    public void Swap_WhenALaterTargetIsLocked_LeavesEveryTargetUnchanged()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8")
            .LockFile(TargetB);                      // the game is running

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, BothTargets);

        Assert.False(result.Success);
        Assert.Equal(SwapFailure.FileInUse, result.Failure);
        Assert.Equal(TargetB, result.FailedPath);
        Assert.False(result.RollbackIncomplete);

        Assert.Equal("dlss-original-a", fileSystem.ReadFile(TargetA));
        Assert.Equal("dlss-original-b", fileSystem.ReadFile(TargetB));
    }

    /// <summary>
    /// A failed swap must not leave backups lying around either. A stray backup makes the app offer
    /// a reset for a game that was never swapped.
    /// </summary>
    [Fact]
    public void Swap_WhenALaterTargetIsLocked_LeavesNoBackupsBehind()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8")
            .LockFile(TargetB);

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, BothTargets);

        Assert.False(result.Success);
        Assert.Empty(result.CreatedBackups);
        Assert.False(fileSystem.FileExists(BackupA));
        Assert.False(fileSystem.FileExists(BackupB));
    }

    [Fact]
    public void Swap_WhenTheGameDirectoryIsNotWritable_ChangesNothingAndReportsAccessDenied()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8")
            .DenyWritesTo(@"C:\Games\Example\Engine\Binaries");

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, BothTargets);

        Assert.False(result.Success);
        Assert.Equal(SwapFailure.AccessDenied, result.Failure);
        Assert.Equal("dlss-original-a", fileSystem.ReadFile(TargetA));
        Assert.Equal("dlss-original-b", fileSystem.ReadFile(TargetB));
        Assert.False(fileSystem.FileExists(BackupA));
    }

    [Fact]
    public void Swap_WhenTheDownloadedDllIsMissing_FailsWithoutTouchingTheGame()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-original-a");

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, new[] { TargetA });

        Assert.False(result.Success);
        Assert.Equal(SwapFailure.SourceMissing, result.Failure);
        Assert.Equal("dlss-original-a", fileSystem.ReadFile(TargetA));
    }

    [Fact]
    public void Swap_WithNoTargets_ReportsNoTargets()
    {
        var fileSystem = new FakeFileSystem().AddFile(DownloadedDll, "dlss-3.8");

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, new string[0]);

        Assert.False(result.Success);
        Assert.Equal(SwapFailure.NoTargets, result.Failure);
    }

    #endregion

    #region A failed reset changes nothing

    /// <summary>
    /// Regression: reset used to mutate its in-memory asset list inside the restore loop but only
    /// write to the database afterwards, so a failure part way through left memory and the database
    /// disagreeing about which dll was installed.
    /// </summary>
    [Fact]
    public void Reset_WhenALaterTargetIsLocked_LeavesEveryTargetUnchanged()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-3.8")
            .AddFile(BackupA, "dlss-original-a")
            .AddFile(TargetB, "dlss-3.8")
            .AddFile(BackupB, "dlss-original-b")
            .LockFile(TargetB);

        var result = new DllSwapExecutor(fileSystem).Reset(BothTargets);

        Assert.False(result.Success);
        Assert.Equal(SwapFailure.FileInUse, result.Failure);
        Assert.False(result.RollbackIncomplete);

        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetA));
        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetB));
    }

    /// <summary>A failed reset must keep the backups, they are the only copy of the original dll.</summary>
    [Fact]
    public void Reset_WhenALaterTargetIsLocked_KeepsEveryBackup()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-3.8")
            .AddFile(BackupA, "dlss-original-a")
            .AddFile(TargetB, "dlss-3.8")
            .AddFile(BackupB, "dlss-original-b")
            .LockFile(TargetB);

        new DllSwapExecutor(fileSystem).Reset(BothTargets);

        Assert.Equal("dlss-original-a", fileSystem.ReadFile(BackupA));
        Assert.Equal("dlss-original-b", fileSystem.ReadFile(BackupB));
    }

    [Fact]
    public void Reset_WhenOneTargetHasNoBackup_ChangesNothing()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-3.8")
            .AddFile(BackupA, "dlss-original-a")
            .AddFile(TargetB, "dlss-3.8");

        var result = new DllSwapExecutor(fileSystem).Reset(BothTargets);

        Assert.False(result.Success);
        Assert.Equal(SwapFailure.BackupMissing, result.Failure);
        Assert.Equal(BackupB, result.FailedPath);

        // Crucially it does not restore A and call that a success.
        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetA));
        Assert.True(fileSystem.FileExists(BackupA));
    }

    #endregion

    #region Housekeeping

    [Fact]
    public void Swap_OnSuccess_LeavesNoTemporaryFiles()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8");

        Assert.True(new DllSwapExecutor(fileSystem).Swap(DownloadedDll, BothTargets).Success);

        AssertNoTemporaryFiles(fileSystem);
    }

    [Fact]
    public void Swap_OnFailure_LeavesNoTemporaryFiles()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8")
            .LockFile(TargetB);

        Assert.False(new DllSwapExecutor(fileSystem).Swap(DownloadedDll, BothTargets).Success);

        AssertNoTemporaryFiles(fileSystem);
    }

    [Fact]
    public void Swap_OnSuccess_ReportsEveryReplacedTarget()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8");

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, BothTargets);

        Assert.True(result.Success);
        Assert.Equal(BothTargets, result.ReplacedPaths);
        Assert.Equal(2, result.CreatedBackups.Count);
        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetA));
        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetB));
    }

    static void AssertNoTemporaryFiles(FakeFileSystem fileSystem)
    {
        var leftovers = fileSystem.AllPaths
            .Where(x => x.EndsWith(DllSwapExecutor.StagedSuffix) || x.EndsWith(DllSwapExecutor.PreviousSuffix))
            .ToList();

        Assert.Empty(leftovers);
    }

    #endregion
}
