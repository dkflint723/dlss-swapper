using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers merging a freshly scanned game onto the one already in the library. Getting "did
/// anything change" wrong either writes to the database on every scan or never saves a real change.
/// </summary>
[Collection(ManifestCollection.Name)]
public class GameUpdateFromGameTests
{
    static GameAsset Asset(string gameId, GameAssetType assetType, string version = "310.1.0.0")
    {
        return new GameAsset()
        {
            Id = gameId,
            AssetType = assetType,
            Path = $@"C:\game\{assetType}.dll",
            Version = version,
            Size = 1024,
            Hash = string.Empty,
        };
    }

    [Fact]
    public void MergingAnIdenticalGameReportsNoChange()
    {
        var existing = new TestGame("merge_1");
        existing.InstallPath = @"C:\game";

        var scanned = new TestGame("merge_1");
        scanned.InstallPath = @"C:\game";

        Assert.False(existing.UpdateFromGame(scanned));
    }

    [Fact]
    public void ARenamedGameReportsAChangeAndTakesTheNewTitle()
    {
        var existing = new TestGame("merge_2");
        var scanned = new TestGame("merge_2");
        scanned.Title = "Resident Evil Requiem";

        Assert.True(existing.UpdateFromGame(scanned));
        Assert.Equal("Resident Evil Requiem", existing.Title);
    }

    [Fact]
    public void AMovedGameTakesTheNewInstallPath()
    {
        var existing = new TestGame("merge_3");
        existing.InstallPath = @"C:\games\thegame";

        var scanned = new TestGame("merge_3");
        scanned.InstallPath = @"D:\games\thegame";

        Assert.True(existing.UpdateFromGame(scanned));
        Assert.Equal(@"D:\games\thegame", existing.InstallPath);
    }

    [Fact]
    public void ADllAppearingInAScanIsCarriedOver()
    {
        using var manifest = new ManifestScope();

        var existing = new TestGame("merge_4");
        existing.InstallPath = @"C:\game";

        var scanned = new TestGame("merge_4");
        scanned.InstallPath = @"C:\game";
        var dlss = Asset(scanned.ID, GameAssetType.DLSS);
        scanned.GameAssets.Add(dlss);
        scanned.UpdateCurrentDLLsFromGameAssets();

        Assert.True(existing.UpdateFromGame(scanned));
        Assert.Same(dlss, existing.GetAssetSlot(GameAssetType.DLSS)!.CurrentAsset);
    }

    [Fact]
    public void ADllDisappearingFromAScanClearsTheSlot()
    {
        using var manifest = new ManifestScope();

        var existing = new TestGame("merge_5");
        existing.InstallPath = @"C:\game";
        existing.GameAssets.Add(Asset(existing.ID, GameAssetType.DLSS));
        existing.UpdateCurrentDLLsFromGameAssets();

        var scanned = new TestGame("merge_5");
        scanned.InstallPath = @"C:\game";

        Assert.True(existing.UpdateFromGame(scanned));
        Assert.Null(existing.GetAssetSlot(GameAssetType.DLSS)!.CurrentAsset);
    }

    [Fact]
    public void NotesAndFavouriteAreNotOverwrittenByAScan()
    {
        // A scan knows where the game is, not what the user wrote about it.
        var existing = new TestGame("merge_6");
        existing.Notes = "Needs 310.6, crashes on 310.7";
        existing.IsFavourite = true;

        var scanned = new TestGame("merge_6");

        existing.UpdateFromGame(scanned);

        Assert.Equal("Needs 310.6, crashes on 310.7", existing.Notes);
        Assert.True(existing.IsFavourite);
    }

    [Fact]
    public void MergingTwiceReportsChangeOnlyTheFirstTime()
    {
        var existing = new TestGame("merge_7");

        var scanned = new TestGame("merge_7");
        scanned.Title = "Pragmata";
        scanned.InstallPath = @"D:\games\pragmata";

        Assert.True(existing.UpdateFromGame(scanned));
        Assert.False(existing.UpdateFromGame(scanned));
    }
}
