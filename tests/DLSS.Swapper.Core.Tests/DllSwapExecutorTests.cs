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

    #region The whole life of a backup

    /// <summary>
    /// Swapping twice and then resetting has to land on the dll the game shipped with, not on
    /// whatever was installed by the swap before last.
    /// </summary>
    /// <remarks>
    /// This is the bug class that cost a user their chosen cover in the cover scan, where a second
    /// apply overwrote the backup with the thing it had just written. The executor is built not to:
    /// EnsureBackup returns early when a backup already exists. Written down as a test because that
    /// early return is one line and reads like an optimisation.
    /// </remarks>
    [Fact]
    public void SwapTwiceThenReset_ReturnsTheGameToTheDllItShippedWith()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-as-shipped")
            .AddFile(DownloadedDll, "dlss-3.8");

        var executor = new DllSwapExecutor(fileSystem);
        var targets = new[] { TargetA };

        Assert.True(executor.Swap(DownloadedDll, targets).Success);

        // A second swap, to a different version, exactly as somebody updating would do.
        fileSystem.AddFile(DownloadedDll, "dlss-3.10");
        Assert.True(executor.Swap(DownloadedDll, targets).Success);
        Assert.Equal("dlss-3.10", fileSystem.ReadFile(TargetA));

        // The backup still holds the original rather than 3.8.
        Assert.Equal("dlss-as-shipped", fileSystem.ReadFile(BackupA));

        Assert.True(executor.Reset(targets).Success);
        Assert.Equal("dlss-as-shipped", fileSystem.ReadFile(TargetA));
    }

    /// <summary>
    /// Reset consumes the backup, so the swap after it has to make a new one.
    /// </summary>
    /// <remarks>
    /// If Reset ever left the backup behind, EnsureBackup would find it and skip - and the next
    /// reset would restore a dll two swaps out of date while reporting success.
    /// </remarks>
    [Fact]
    public void ResetThenSwapThenReset_StillReturnsTheGameToItsOriginal()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-as-shipped")
            .AddFile(DownloadedDll, "dlss-3.8");

        var executor = new DllSwapExecutor(fileSystem);
        var targets = new[] { TargetA };

        Assert.True(executor.Swap(DownloadedDll, targets).Success);
        Assert.True(executor.Reset(targets).Success);
        Assert.False(fileSystem.FileExists(BackupA));

        var second = executor.Swap(DownloadedDll, targets);
        Assert.True(second.Success);
        Assert.Equal(BackupA, Assert.Single(second.CreatedBackups).BackupPath);
        Assert.Equal("dlss-as-shipped", fileSystem.ReadFile(BackupA));

        Assert.True(executor.Reset(targets).Success);
        Assert.Equal("dlss-as-shipped", fileSystem.ReadFile(TargetA));
    }

    /// <summary>
    /// A second reset has nothing to restore from and must say so rather than touch the game.
    /// </summary>
    [Fact]
    public void ResetTwice_ReportsTheMissingBackupAndLeavesTheGameAlone()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-as-shipped")
            .AddFile(DownloadedDll, "dlss-3.8");

        var executor = new DllSwapExecutor(fileSystem);
        var targets = new[] { TargetA };

        Assert.True(executor.Swap(DownloadedDll, targets).Success);
        Assert.True(executor.Reset(targets).Success);

        var second = executor.Reset(targets);

        Assert.False(second.Success);
        Assert.Equal(SwapFailure.BackupMissing, second.Failure);
        Assert.Equal("dlss-as-shipped", fileSystem.ReadFile(TargetA));
    }

    /// <summary>
    /// The same location listed twice is one location.
    /// </summary>
    /// <remarks>
    /// Nothing in the app is known to produce a duplicate today - the paths come from a directory
    /// walk - but a junction, a symlink, or a second source of paths would, and the phases are three
    /// separate loops over the list. Committing a path twice deletes the previous contents it had
    /// just kept and then finds its staged file already consumed, which fails the whole swap and
    /// reports an incomplete rollback for something the caller merely said twice.
    /// </remarks>
    [Fact]
    public void Swap_WhenTheSameTargetIsListedTwice_TreatsItAsOne()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-as-shipped")
            .AddFile(DownloadedDll, "dlss-3.8");

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, new[] { TargetA, TargetA });

        Assert.True(result.Success);
        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetA));
        Assert.Equal("dlss-as-shipped", fileSystem.ReadFile(BackupA));
        Assert.Equal(BackupA, Assert.Single(result.CreatedBackups).BackupPath);
        Assert.Equal(TargetA, Assert.Single(result.ReplacedPaths));
        AssertNoTemporaryFiles(fileSystem);
    }

    /// <summary>
    /// The same location listed twice, on the way back.
    /// </summary>
    [Fact]
    public void Reset_WhenTheSameTargetIsListedTwice_TreatsItAsOne()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-3.8")
            .AddFile(BackupA, "dlss-as-shipped");

        var result = new DllSwapExecutor(fileSystem).Reset(new[] { TargetA, TargetA });

        Assert.True(result.Success);
        Assert.Equal("dlss-as-shipped", fileSystem.ReadFile(TargetA));
        Assert.False(fileSystem.FileExists(BackupA));
        AssertNoTemporaryFiles(fileSystem);
    }

    #endregion

    #region A target that was not there to begin with

    /// <summary>
    /// A target that did not exist is created by the swap, and a failed swap has to take it away
    /// again.
    /// </summary>
    /// <remarks>
    /// Commit has two branches. When the target exists it is replaced and its previous contents are
    /// kept for rollback. When it does not, the staged file is moved into place and nothing is
    /// recorded - "nothing to preserve, so nothing to roll back to", which is true of the contents
    /// and wrong about the path. The file is still something this swap created, and leaving it
    /// behind after reporting failure puts a dll in a game folder that the records say is not there.
    ///
    /// Reachable: a game patch deletes one of two dll locations, no rescan has run yet, so the
    /// location is still in the game's recorded assets and is handed to the swap as a target.
    /// </remarks>
    [Fact]
    public void Swap_WhenAMissingTargetWasCreatedAndALaterOneFails_TakesTheCreatedOneAway()
    {
        var fileSystem = new FakeFileSystem()
            // TargetA is gone from disk but still recorded, and still has its backup.
            .AddFile(BackupA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(BackupB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8")
            .LockFile(TargetB);

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, BothTargets);

        Assert.False(result.Success);

        // The one it created has to be gone again, or the game is left holding a dll the app just
        // told the user it had not written.
        Assert.False(fileSystem.FileExists(TargetA));

        // And the untouched one is still its original.
        Assert.Equal("dlss-original-b", fileSystem.ReadFile(TargetB));

        AssertNoTemporaryFiles(fileSystem);
    }

    /// <summary>The same swap, succeeding: the created target keeps the new dll.</summary>
    [Fact]
    public void Swap_WhenAMissingTargetIsCreatedAndAllSucceed_KeepsIt()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(BackupA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8");

        var result = new DllSwapExecutor(fileSystem).Swap(DownloadedDll, BothTargets);

        Assert.True(result.Success);
        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetA));
        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetB));

        // The backup that was already there is still the original.
        Assert.Equal("dlss-original-a", fileSystem.ReadFile(BackupA));
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
