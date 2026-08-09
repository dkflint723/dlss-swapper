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
