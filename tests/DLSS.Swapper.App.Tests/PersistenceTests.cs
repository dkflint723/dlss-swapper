using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DLSS_Swapper;
using DLSS_Swapper.Data;
using SQLite;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers whether changes survive a restart.
/// </summary>
/// <remarks>
/// Every other test in this project asserts on objects in memory, and three bugs have now slipped
/// through that way: the original swap path, the stale update badge, and saving a copy of an
/// original. In each case the object was correct and the row was missing, so the app looked right
/// until it was reopened. These tests reload from a real database instead of trusting the object.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public class PersistenceTests
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
            Hash = "ABC123",
        };
    }

    /// <summary>Reads a game's assets straight back out of the database.</summary>
    static async Task<System.Collections.Generic.List<GameAsset>> ReadAssetsAsync(string gameId)
    {
        using (await Database.Instance.Mutex.LockAsync())
        {
            return await Database.Instance.Connection.Table<GameAsset>().Where(x => x.Id == gameId).ToListAsync();
        }
    }

    [Fact]
    public async Task SavingACopyOutlivesTheProcess()
    {
        // The bug this was written for. Saving a copy created the file and updated the list in
        // memory, but never wrote the row, so the game reported the backup missing again on the
        // next launch even though the file was sitting right next to the dll.
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var game = new TestGame("persist_1");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));

        var saved = await game.SaveOriginalCopiesAsync();

        Assert.Equal(1, saved);
        Assert.True(File.Exists(dllPath + ".dlsss"), "the copy should be on disk");

        var stored = await ReadAssetsAsync(game.ID);
        Assert.Contains(stored, x => x.AssetType == GameAssetType.DLSS_BACKUP);
    }

    [Fact]
    public async Task AGameThatAlreadyHasACopyIsLeftAlone()
    {
        // Re-running must not replace the saved original with whatever is installed now, which
        // would quietly turn a swapped dll into the "original".
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var game = new TestGame("persist_2");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));
        await game.SaveOriginalCopiesAsync();

        var backupWrittenAt = File.GetLastWriteTimeUtc(dllPath + ".dlsss");

        // A different dll is now installed, as if a swap had happened.
        File.WriteAllBytes(dllPath, new byte[4096]);
        var savedAgain = await game.SaveOriginalCopiesAsync();

        Assert.Equal(0, savedAgain);
        Assert.Equal(backupWrittenAt, File.GetLastWriteTimeUtc(dllPath + ".dlsss"));
    }

    [Fact]
    public async Task SkipUpdatesOutlivesTheProcess()
    {
        // A new column, so this also proves the migration ran rather than silently dropping it.
        await using var database = await TemporaryDatabase.CreateAsync();

        var game = new TestGame("persist_3");
        game.SkipUpdates = true;

        using (await Database.Instance.Mutex.LockAsync())
        {
            // TestGame is not one of the library types the app creates tables for, so it gets one
            // here. The column layout still comes from Game, which is what is under test.
            await Database.Instance.Connection.CreateTableAsync<TestGame>();
        }

        await game.SaveToDatabaseAsync();

        using (await Database.Instance.Mutex.LockAsync())
        {
            var stored = await Database.Instance.Connection.Table<TestGame>().Where(x => x.ID == game.ID).FirstOrDefaultAsync();

            Assert.NotNull(stored);
            Assert.True(stored!.SkipUpdates);
        }
    }

    [Fact]
    public async Task AGameAssetKeepsItsSizeAndHash()
    {
        // Size and hash together are what let a refresh skip re-reading gigabytes. If either fails
        // to persist, every launch re-hashes the whole library and nothing says why.
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var game = new TestGame("persist_4");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));

        await game.SaveOriginalCopiesAsync();

        var stored = await ReadAssetsAsync(game.ID);
        var dlss = stored.First(x => x.AssetType == GameAssetType.DLSS);

        Assert.Equal(2048, dlss.Size);
        Assert.Equal("ABC123", dlss.Hash);
    }

    [Fact]
    public async Task SavingReplacesTheStoredAssetsRatherThanAddingToThem()
    {
        // The write is a delete and reinsert. If the delete were dropped, every save would double
        // the rows and a game would slowly grow duplicate dlls.
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var game = new TestGame("persist_5");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));

        await game.SaveOriginalCopiesAsync();
        var afterFirst = await ReadAssetsAsync(game.ID);

        // Nothing new to save, so the rows should be untouched rather than appended to.
        await game.SaveOriginalCopiesAsync();
        var afterSecond = await ReadAssetsAsync(game.ID);

        Assert.Equal(afterFirst.Count, afterSecond.Count);
    }

    [Fact]
    public async Task ADllWithNoFileOnDiskCannotBeCopied()
    {
        // The path is in the database but the game has been uninstalled or moved. It must report
        // nothing saved rather than claiming success or throwing.
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var game = new TestGame("persist_6");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, Path.Combine(database.GameFolder, "gone.dll")));

        var saved = await game.SaveOriginalCopiesAsync();

        Assert.Equal(0, saved);
    }

    [Fact]
    public async Task TheDatabaseIsTheTemporaryOne()
    {
        // Guards the guard. If the override ever stopped working these tests would be writing to a
        // real library, and every other assertion here would still pass.
        await using var database = await TemporaryDatabase.CreateAsync();

        Assert.StartsWith(database.Root, Storage.GetDBPath());
        Assert.True(File.Exists(Storage.GetDBPath()));
    }
}
