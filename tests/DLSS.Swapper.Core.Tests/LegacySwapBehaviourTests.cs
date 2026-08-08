using System;
using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Swapping;
using Xunit;

namespace DLSS_Swapper.Tests;

/// <summary>
/// Executable documentation of the file handling that <see cref="DllSwapExecutor"/> replaced.
/// </summary>
/// <remarks>
/// <para>
/// These tests assert the <em>broken</em> outcomes on purpose. Without them the executor's own tests
/// would pass just as happily against an implementation that never had the problem, and there would
/// be nothing showing why the extra machinery is there.
/// </para>
/// <para>
/// <see cref="LegacySwap"/> reproduces the file operations that used to live in Game.UpdateDllAsync:
/// a whole-set check for whether backups were needed, followed by a loop of overwriting copies that
/// returned on the first failure without undoing anything.
/// </para>
/// </remarks>
public class LegacySwapBehaviourTests
{
    const string TargetA = @"C:\Games\Example\Binaries\nvngx_dlss.dll";
    const string TargetB = @"C:\Games\Example\Engine\Binaries\nvngx_dlss.dll";
    const string BackupA = TargetA + DllSwapExecutor.BackupSuffix;
    const string BackupB = TargetB + DllSwapExecutor.BackupSuffix;
    const string DownloadedDll = @"C:\Users\Example\AppData\Local\dlss_swapper\dlls\dlss_3.8\nvngx_dlss.dll";

    static IReadOnlyList<string> BothTargets => new[] { TargetA, TargetB };

    static bool LegacySwap(IFileSystem fileSystem, string sourcePath, IReadOnlyList<string> targetPaths)
    {
        // The backup decision was made once for the whole asset type, not once per file.
        var anyBackupExists = targetPaths.Any(x => fileSystem.FileExists(x + DllSwapExecutor.BackupSuffix));

        if (anyBackupExists == false)
        {
            foreach (var targetPath in targetPaths)
            {
                var backupPath = targetPath + DllSwapExecutor.BackupSuffix;
                if (fileSystem.FileExists(backupPath) == false)
                {
                    fileSystem.Copy(targetPath, backupPath, false);
                }
            }
        }

        foreach (var targetPath in targetPaths)
        {
            try
            {
                fileSystem.Copy(sourcePath, targetPath, true);
            }
            catch (Exception)
            {
                // Returned straight to the caller, leaving earlier targets already overwritten.
                return false;
            }
        }

        return true;
    }

    [Fact]
    public void Legacy_WhenOnlyOneTargetHasABackup_DestroysTheOtherOriginal()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-3.5")
            .AddFile(BackupA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8");

        var succeeded = LegacySwap(fileSystem, DownloadedDll, BothTargets);

        Assert.True(succeeded);

        // One existing backup suppressed the backup step for every location, so B's original dll was
        // overwritten with nothing kept. There is no way back to "dlss-original-b" from here.
        Assert.False(fileSystem.FileExists(BackupB));
        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetB));
    }

    [Fact]
    public void Legacy_WhenALaterTargetIsLocked_ReportsFailureButLeavesTheFirstTargetSwapped()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(TargetA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8")
            .LockFile(TargetB);

        var succeeded = LegacySwap(fileSystem, DownloadedDll, BothTargets);

        Assert.False(succeeded);

        // The caller was told the swap failed and so wrote nothing to its database, but A really was
        // swapped. The next rescan sees A as changed by something outside the app and deletes its
        // backup, which is where the original dll is actually lost.
        Assert.Equal("dlss-3.8", fileSystem.ReadFile(TargetA));
        Assert.Equal("dlss-original-b", fileSystem.ReadFile(TargetB));
    }

    /// <summary>
    /// The same two scenarios through <see cref="DllSwapExecutor"/>, side by side, so the difference
    /// is visible in one place.
    /// </summary>
    [Fact]
    public void Executor_HandlesBothScenariosWithoutLosingAnOriginal()
    {
        var mixedBackups = new FakeFileSystem()
            .AddFile(TargetA, "dlss-3.5")
            .AddFile(BackupA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8");

        Assert.True(new DllSwapExecutor(mixedBackups).Swap(DownloadedDll, BothTargets).Success);
        Assert.Equal("dlss-original-b", mixedBackups.ReadFile(BackupB));

        var lockedTarget = new FakeFileSystem()
            .AddFile(TargetA, "dlss-original-a")
            .AddFile(TargetB, "dlss-original-b")
            .AddFile(DownloadedDll, "dlss-3.8")
            .LockFile(TargetB);

        Assert.False(new DllSwapExecutor(lockedTarget).Swap(DownloadedDll, BothTargets).Success);
        Assert.Equal("dlss-original-a", lockedTarget.ReadFile(TargetA));
    }
}
