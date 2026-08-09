using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers that a game marked never update cannot have its dlls changed at all.
/// </summary>
/// <remarks>
/// The setting started as "leave this out of bulk updates", which turned out to be a weak promise:
/// a game excluded because a modified dll gets it flagged by anti cheat is no safer if the swap can
/// still be done by hand from its own page. These assert the rule holds at the choke point every
/// caller goes through, rather than only where the buttons happen to be hidden.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public class SwapLockTests
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
    public async Task ALockedGameRefusesAReset()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var game = new TestGame("lock_1");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP, dllPath + ".dlsss"));
        game.SkipUpdates = true;

        var result = await game.ResetDllAsync(GameAssetType.DLSS);

        Assert.False(result.Success);
        Assert.DoesNotContain("LangResourceError", result.Message);
    }

    [Fact]
    public async Task ALockedGameRefusesASwap()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var game = new TestGame("lock_2");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));
        game.SkipUpdates = true;

        var record = manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var result = await game.UpdateDllAsync(record);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task TheRefusalSaysHowToUndoIt()
    {
        // A rule the user cannot find the switch for is indistinguishable from a bug.
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var game = new TestGame("lock_3");
        game.SkipUpdates = true;

        var result = await game.ResetDllAsync(GameAssetType.DLSS);

        Assert.Contains("turn", result.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheLockIsCheckedBeforeAnythingElse()
    {
        // A locked game with no backup should be refused for being locked, not for having no
        // backup, or the message sends the user to fix the wrong thing.
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var game = new TestGame("lock_4");
        game.SkipUpdates = true;

        var result = await game.ResetDllAsync(GameAssetType.DLSS);

        Assert.False(result.Success);
        Assert.DoesNotContain("backup", result.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnlockingRestoresTheAbilityToReset()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var game = new TestGame("lock_5");
        game.SkipUpdates = true;

        var locked = await game.ResetDllAsync(GameAssetType.DLSS);
        Assert.False(locked.Success);

        game.SkipUpdates = false;
        var unlocked = await game.ResetDllAsync(GameAssetType.DLSS);

        // Still fails, because there is no backup to restore, but for that reason rather than the
        // lock. The point is that the lock is no longer what stops it.
        Assert.NotEqual(locked.Message, unlocked.Message);
    }

    [Fact]
    public async Task ALockedGameKeepsItsFilesUntouched()
    {
        // The whole point: the dll on disk is not to be written to.
        await using var database = await TemporaryDatabase.CreateAsync();
        using var manifest = new ManifestScope();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var before = File.GetLastWriteTimeUtc(dllPath);

        var game = new TestGame("lock_6");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP, dllPath + ".dlsss"));
        game.SkipUpdates = true;

        await game.ResetDllAsync(GameAssetType.DLSS);

        Assert.Equal(before, File.GetLastWriteTimeUtc(dllPath));
    }
}
