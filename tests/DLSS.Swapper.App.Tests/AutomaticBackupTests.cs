using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the rule automatic backups rest on: an existing saved original is never replaced.
/// </summary>
/// <remarks>
/// Backups used to be taken only the first time a game was seen, so a dll found later was detected
/// and left unprotected. Widening that to every newly found dll is only safe because of this rule:
/// without it, backing up a dll that had already been swapped would promote the swapped version to
/// "original" and destroy the only copy of the real one.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public class AutomaticBackupTests
{
    static GameAsset Asset(string gameId, GameAssetType assetType, string path)
    {
        return new GameAsset()
        {
            Id = gameId,
            AssetType = assetType,
            Path = path,
            Version = "310.1.0.0",
            Size = 2048,
            Hash = string.Empty,
        };
    }

    [Fact]
    public async Task ADllWithNoCopyGetsOne()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var game = new TestGame("backup_1");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));

        var saved = await game.SaveOriginalCopiesAsync();

        Assert.Equal(1, saved);
        Assert.True(File.Exists(dllPath + ".dlsss"));
    }

    [Fact]
    public async Task AnExistingCopyIsNeverReplaced()
    {
        // The rule the whole change rests on. If a swapped dll could overwrite the saved original,
        // the user would lose the only copy of the file they actually shipped with.
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var game = new TestGame("backup_2");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));
        await game.SaveOriginalCopiesAsync();

        var originalBytes = File.ReadAllBytes(dllPath + ".dlsss");

        // The installed dll is replaced, as a swap would do, and a backup is attempted again.
        File.WriteAllBytes(dllPath, new byte[9999]);
        await game.SaveOriginalCopiesAsync();

        Assert.Equal(originalBytes, File.ReadAllBytes(dllPath + ".dlsss"));
    }

    [Fact]
    public async Task OnlyTheDllsWithoutACopyAreBackedUp()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dlssPath = database.WriteFakeDll("nvngx_dlss.dll");
        var xessPath = database.WriteFakeDll("libxess.dll");

        var game = new TestGame("backup_3");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dlssPath));
        await game.SaveOriginalCopiesAsync();

        // XeSS arrives later, as a patch would deliver it.
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS, xessPath));
        var saved = await game.SaveOriginalCopiesAsync();

        Assert.Equal(1, saved);
        Assert.True(File.Exists(xessPath + ".dlsss"));
    }

    /// <summary>
    /// A game shipping the same dll in two folders needs a copy of both.
    /// </summary>
    /// <remarks>
    /// The gate used to ask whether any dll of the same TYPE had a backup, so one copy answered for
    /// every location: the first was saved, the second skipped, and the method reported success.
    /// The row then read as protected while that second location had no original saved anywhere.
    /// </remarks>
    [Fact]
    public async Task EveryLocationOfADllGetsItsOwnCopy()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var firstPath = database.WriteFakeDll("nvngx_dlss.dll");

        var engineFolder = Path.Combine(database.GameFolder, "Engine", "Binaries");
        Directory.CreateDirectory(engineFolder);
        var secondPath = Path.Combine(engineFolder, "nvngx_dlss.dll");
        File.Copy(firstPath, secondPath);

        var game = new TestGame("backup_two_locations");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, firstPath));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, secondPath));

        var saved = await game.SaveOriginalCopiesAsync();

        Assert.Equal(2, saved);
        Assert.True(File.Exists(firstPath + ".dlsss"));
        Assert.True(File.Exists(secondPath + ".dlsss"));
    }

    /// <summary>
    /// And the row has to say so while only one of the two is covered.
    /// </summary>
    /// <remarks>
    /// The same type-wide question was asked in three places at once, so the list, the row and the
    /// sidebar all agreed a half protected game was fully protected. They read one rule now.
    /// </remarks>
    [Fact]
    public async Task AGameWithOnlyOneOfTwoLocationsCoveredStillReportsAMissingCopy()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var firstPath = database.WriteFakeDll("nvngx_dlss.dll");

        var engineFolder = Path.Combine(database.GameFolder, "Engine", "Binaries");
        Directory.CreateDirectory(engineFolder);
        var secondPath = Path.Combine(engineFolder, "nvngx_dlss.dll");
        File.Copy(firstPath, secondPath);

        var game = new TestGame("backup_half_covered");
        var first = Asset(game.ID, GameAssetType.DLSS, firstPath);
        var second = Asset(game.ID, GameAssetType.DLSS, secondPath);
        game.GameAssets.Add(first);
        game.GameAssets.Add(second);

        // Only the first location has its original saved.
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP, firstPath + ".dlsss"));

        Assert.True(game.HasSavedOriginal(first));
        Assert.False(game.HasSavedOriginal(second));
        Assert.True(GameFilters.IsMissingABackup(game));
    }

    /// <summary>
    /// A scan that removes a stale backup must not write a new one from the dll on disk.
    /// </summary>
    /// <remarks>
    /// The scan deletes the saved original when the installed dll no longer matches the version it
    /// recorded, on the grounds that a game updated past the version you swapped to should not read
    /// as a downgrade. It then ran the automatic backup, whose only guard is that no backup file
    /// exists - which the delete had just made true. So the one copy of the dll the game shipped
    /// with was replaced by a copy of whatever was installed now, including a dll this app had
    /// swapped in and failed to record. Reset would have restored the swapped dll and called it a
    /// success.
    /// </remarks>
    [Fact]
    public async Task AScanThatRemovesAStaleCopyDoesNotWriteANewOneFromTheInstalledDll()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var backupPath = dllPath + ".dlsss";

        // What the game shipped with, saved.
        File.Copy(dllPath, backupPath);
        var shippedBytes = File.ReadAllBytes(backupPath);

        // Something replaced the installed dll since the last scan, and the recorded version no
        // longer matches what is on disk. A swap this app made and did not get to record looks
        // exactly like this.
        var swappedBytes = new byte[4096];
        for (var index = 0; index < swappedBytes.Length; index += 1)
        {
            swappedBytes[index] = 0x5A;
        }

        File.WriteAllBytes(dllPath, swappedBytes);

        var game = new TestGame("backup_stale_scan")
        {
            InstallPath = database.GameFolder,

            // Keeps the scan off the cover art path, which has nothing to do with this.
            IsHidden = true,
        };

        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));

        game.ProcessGame(autoSave: false, forceNeedsProcessing: true);
        await WaitForScanAsync(game);

        // Worth stating: the setup is only meaningful because these differ.
        Assert.NotEqual(shippedBytes, swappedBytes);

        // The stale copy is gone, which is the behaviour being kept - and nothing wrote a new
        // "original" from the dll installed now. If the delete branch had not run at all this would
        // fail too, which is what keeps the test honest.
        Assert.False(File.Exists(backupPath));
    }

    /// <summary>
    /// Deleting a user's saved original is recorded before the file goes, not after the scan.
    /// </summary>
    /// <remarks>
    /// The history batch used to be inserted only at the very end of the scan, and only when the
    /// game still had dlls. Anything that threw in between - a locked dll, an unreadable version -
    /// dropped the batch, so the deletion had happened and nothing remembered it. That record is
    /// what a later scan needs to tell "your swap was undone" from "this dll is new to me".
    /// </remarks>
    [Fact]
    public async Task RemovingAStaleCopyIsRecordedEvenIfTheRestOfTheScanFails()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var backupPath = dllPath + ".dlsss";
        File.Copy(dllPath, backupPath);

        // The installed dll no longer matches what was recorded, which is what makes the scan
        // remove the stale copy.
        File.WriteAllBytes(dllPath, new byte[4096]);

        var game = new TestGame("backup_history")
        {
            InstallPath = database.GameFolder,
            IsHidden = true,
        };

        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));

        game.ProcessGame(autoSave: false, forceNeedsProcessing: true);
        await WaitForScanAsync(game);

        Assert.False(File.Exists(backupPath));

        var history = await Database.Instance.Connection.Table<GameHistory>()
            .Where(x => x.GameId == game.ID)
            .ToListAsync();

        Assert.Contains(history, x => x.EventType == GameHistoryEventType.DLLBackupRemoved);
    }

    /// <summary>
    /// ProcessGame reports through a flag rather than a task, so the test waits on the flag.
    /// </summary>
    static async Task WaitForScanAsync(Game game)
    {
        for (var attempt = 0; attempt < 200; attempt += 1)
        {
            if (game.Processing == false)
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail("The scan did not finish.");
    }

    [Fact]
    public async Task ACopyIsAFaithfulCopy()
    {
        // It is the file the user gets back when they revert, so it has to be byte identical.
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll", bytes: 4096);
        var game = new TestGame("backup_4");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));

        await game.SaveOriginalCopiesAsync();

        Assert.Equal(File.ReadAllBytes(dllPath), File.ReadAllBytes(dllPath + ".dlsss"));
    }
}
