using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers noticing that a game has gained a dll since it was last processed.
/// </summary>
/// <remarks>
/// A game was processed only the first time it was ever seen, so one that gained dlls in a patch
/// was never looked at again. DOOM: The Dark Ages shipped three DLSS dlls and the app went on
/// offering only its FSR and XeSS. Nothing on screen suggested anything was missing, which is what
/// made it survive: an undetected dll and a dll the game does not have looked identical.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public class UnrecordedDllTests
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
    public async Task ADllOnDiskThatIsNotRecordedIsNoticed()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var game = new TestGame("unrecorded_1");
        game.InstallPath = database.GameFolder;

        Assert.True(game.HasUnrecordedDlls());
    }

    [Fact]
    public async Task ADllThatIsAlreadyRecordedIsNotNoticed()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        var game = new TestGame("unrecorded_2");
        game.InstallPath = database.GameFolder;
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));

        Assert.False(game.HasUnrecordedDlls());
    }

    [Fact]
    public async Task ANewDllBesideRecordedOnesIsNoticed()
    {
        // The DOOM case exactly: some dlls known, a new one arrives in a patch.
        await using var database = await TemporaryDatabase.CreateAsync();

        var known = database.WriteFakeDll("libxess.dll");
        database.WriteFakeDll("nvngx_dlss.dll");

        var game = new TestGame("unrecorded_3");
        game.InstallPath = database.GameFolder;
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS, known));

        Assert.True(game.HasUnrecordedDlls());
    }

    [Fact]
    public async Task ADllInASubfolderIsNoticed()
    {
        // DOOM keeps its DLSS dlls in streamline\production, three levels down.
        await using var database = await TemporaryDatabase.CreateAsync();

        var nested = Path.Combine(database.GameFolder, "streamline", "production");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "nvngx_dlss.dll"), new byte[2048]);

        var game = new TestGame("unrecorded_4");
        game.InstallPath = database.GameFolder;

        Assert.True(game.HasUnrecordedDlls());
    }

    [Fact]
    public async Task UnrelatedDllsAreIgnored()
    {
        // A game folder is full of dlls. Only the ones this app manages count, or every game would
        // be reprocessed on every launch.
        await using var database = await TemporaryDatabase.CreateAsync();

        File.WriteAllBytes(Path.Combine(database.GameFolder, "d3d12.dll"), new byte[512]);
        File.WriteAllBytes(Path.Combine(database.GameFolder, "physx.dll"), new byte[512]);

        var game = new TestGame("unrecorded_5");
        game.InstallPath = database.GameFolder;

        Assert.False(game.HasUnrecordedDlls());
    }

    [Fact]
    public async Task AGameWhoseFolderIsGoneIsNotNoticed()
    {
        // Uninstalled, or on a drive that is not mounted. Reprocessing it would achieve nothing.
        await using var database = await TemporaryDatabase.CreateAsync();

        var game = new TestGame("unrecorded_6");
        game.InstallPath = Path.Combine(database.Root, "not-here");

        Assert.False(game.HasUnrecordedDlls());
    }

    [Fact]
    public async Task AGameWithNoInstallPathIsNotNoticed()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var game = new TestGame("unrecorded_7");
        game.InstallPath = string.Empty;

        Assert.False(game.HasUnrecordedDlls());
    }

    [Fact]
    public async Task TheSameDllInTwoFoldersIsNoticedWhenOnlyOneIsRecorded()
    {
        // A game can carry the same dll in more than one place, and each is separately swappable.
        await using var database = await TemporaryDatabase.CreateAsync();

        var first = database.WriteFakeDll("nvngx_dlss.dll");
        var secondFolder = Path.Combine(database.GameFolder, "bin2");
        Directory.CreateDirectory(secondFolder);
        File.WriteAllBytes(Path.Combine(secondFolder, "nvngx_dlss.dll"), new byte[2048]);

        var game = new TestGame("unrecorded_8");
        game.InstallPath = database.GameFolder;
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, first));

        Assert.True(game.HasUnrecordedDlls());
    }

    [Fact]
    public async Task ABackupBesideARecordedDllDoesNotCount()
    {
        // The .dlsss copy is not a dll the game ships, and it does not end in .dll anyway. If it
        // counted, every backed up game would be reprocessed forever.
        await using var database = await TemporaryDatabase.CreateAsync();

        var dllPath = database.WriteFakeDll("nvngx_dlss.dll");
        File.Copy(dllPath, dllPath + ".dlsss");

        var game = new TestGame("unrecorded_9");
        game.InstallPath = database.GameFolder;
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, dllPath));

        Assert.False(game.HasUnrecordedDlls());
    }
}
