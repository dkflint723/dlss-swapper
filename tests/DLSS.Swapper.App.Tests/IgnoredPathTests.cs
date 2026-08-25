using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Telling the app to ignore a folder must not destroy what is in it.
/// </summary>
/// <remarks>
/// Every library's scan skips a game in an ignored path, and every library then deletes the cached
/// games its scan did not return, on the reasoning that they must have been uninstalled. Deleting a
/// game removes the copies of the dlls it shipped with - deliberately, so an uninstall does not
/// leave them behind. Put together, adding an ignored path destroyed the saved originals of every
/// game underneath it, permanently, which is the opposite of what ignoring a folder asks for.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public class IgnoredPathTests
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
    public async Task DeletingAGameInAnIgnoredPathKeepsItsSavedOriginals()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var backupPath = dllPath + ".dlsss";
        File.Copy(dllPath, backupPath);

        var game = new TestGame("ignored_1") { InstallPath = database.GameFolder };
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP, backupPath));
        await game.SaveToDatabaseAsync();

        using (await Database.Instance.Mutex.LockAsync())
        {
            await Database.Instance.Connection.InsertAllAsync(game.GameAssets, false);
        }

        var previousIgnoredPaths = Settings.Instance.IgnoredPaths;

        try
        {
            // The app stores these with a trailing separator.
            Settings.Instance.IgnoredPaths = new[] { database.GameFolder + Path.DirectorySeparatorChar };

            Assert.True(game.IsInIgnoredPath());

            await game.DeleteAsync();

            // The game leaves the app - that is what ignoring it means...
            using (await Database.Instance.Mutex.LockAsync())
            {
                var rows = await Database.Instance.Connection.Table<GameAsset>()
                    .Where(x => x.Id == game.ID)
                    .ToListAsync();

                Assert.Empty(rows);
            }

            // ...but the copy of the dll it shipped with is still on disk.
            Assert.True(File.Exists(backupPath));
        }
        finally
        {
            Settings.Instance.IgnoredPaths = previousIgnoredPaths;
        }
    }

    /// <summary>
    /// A game that really was uninstalled still has its leftovers cleaned up, which is what the
    /// deletion is for.
    /// </summary>
    [Fact]
    public async Task DeletingAGameThatIsNotIgnoredStillRemovesItsSavedOriginals()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var backupPath = dllPath + ".dlsss";
        File.Copy(dllPath, backupPath);

        var game = new TestGame("ignored_2") { InstallPath = database.GameFolder };
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP, backupPath));
        await game.SaveToDatabaseAsync();

        using (await Database.Instance.Mutex.LockAsync())
        {
            await Database.Instance.Connection.InsertAllAsync(game.GameAssets, false);
        }

        var previousIgnoredPaths = Settings.Instance.IgnoredPaths;

        try
        {
            Settings.Instance.IgnoredPaths = System.Array.Empty<string>();

            Assert.False(game.IsInIgnoredPath());

            await game.DeleteAsync();

            Assert.False(File.Exists(backupPath));
        }
        finally
        {
            Settings.Instance.IgnoredPaths = previousIgnoredPaths;
        }
    }
}
