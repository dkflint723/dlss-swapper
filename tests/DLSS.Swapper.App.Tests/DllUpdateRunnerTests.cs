using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers which dlls "revert all to defaults" would actually touch. This decides both what the
/// confirmation prompt claims and what gets restored, so the two cannot be allowed to disagree.
/// </summary>
public class DllUpdateRunnerTests
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
    public void AGameThatWasNeverSwappedHasNothingToRevert()
    {
        var game = new TestGame("revert_1");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));

        Assert.Empty(DllUpdateRunner.GetRevertableAssetTypes(game));
    }

    [Fact]
    public void ABackupMakesItsOwnTypeRevertable()
    {
        var game = new TestGame("revert_2");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP));

        Assert.Equal(new[] { GameAssetType.DLSS }, DllUpdateRunner.GetRevertableAssetTypes(game));
    }

    [Fact]
    public void OnlyTheTypesWithBackupsAreRevertable()
    {
        var game = new TestGame("revert_3");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS));

        var revertable = DllUpdateRunner.GetRevertableAssetTypes(game);

        Assert.Contains(GameAssetType.DLSS, revertable);
        Assert.DoesNotContain(GameAssetType.XeSS, revertable);
    }

    [Fact]
    public void EachTypeIsListedOnceEvenWithBackupsInSeveralPlaces()
    {
        // A game with the same dll in two folders has two backups, but reverting is one action per
        // type, so counting them twice would overstate what the prompt is about to do.
        var game = new TestGame("revert_4");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP));

        Assert.Equal(new[] { GameAssetType.DLSS }, DllUpdateRunner.GetRevertableAssetTypes(game));
    }

    [Fact]
    public void EveryTypeInTheRegistryCanBeReverted()
    {
        // Backing up is offered for every swappable type, so reverting has to recognise every one.
        var game = new TestGame("revert_5");
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            game.GameAssets.Add(Asset(game.ID, dllTypeDefinition.BackupAssetType));
        }

        Assert.Equal(DllTypes.All.Length, DllUpdateRunner.GetRevertableAssetTypes(game).Count);
    }

    [Fact]
    public void ThePreviewSaysWhatEachDllIsAndWhatItGoesBackTo()
    {
        var game = new TestGame("revert_preview_1");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.2.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP, "310.1.0.0"));

        var row = Assert.Single(DllUpdateRunner.GetRevertPreview(game));

        Assert.Equal(game.Title, row.GameTitle);
        Assert.Equal(ResourceHelper.GetString("General_Name_DLSS"), row.EngineName);
        Assert.Equal("v310.2", row.FromVersion);
        Assert.Equal("v310.1", row.ToVersion);
    }

    [Fact]
    public void ThePreviewHoldsExactlyTheRowsTheRunWouldTouch()
    {
        // The header rule of this file, applied to the preview: the confirmation lists rows from
        // the same source the run reads, so they cannot drift apart.
        var game = new TestGame("revert_preview_2");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS_BACKUP));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_G));

        var preview = DllUpdateRunner.GetRevertPreview(game);

        Assert.Equal(DllUpdateRunner.GetRevertableAssetTypes(game).Count, preview.Count);
        Assert.Equal(2, preview.Count);
    }

    [Fact]
    public void AGameThatWasNeverSwappedHasAnEmptyPreview()
    {
        var game = new TestGame("revert_preview_3");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));

        Assert.Empty(DllUpdateRunner.GetRevertPreview(game));
    }

    [Fact]
    public void ADllDeletedOutsideTheAppStillPreviewsItsRestore()
    {
        // Only the backup remains: the game's own dll was removed by a repair or an update. The
        // row still shows what will come back, with nothing on the "is now" side.
        var game = new TestGame("revert_preview_4");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP, "310.1.0.0"));

        var row = Assert.Single(DllUpdateRunner.GetRevertPreview(game));

        Assert.Equal(string.Empty, row.FromVersion);
        Assert.Equal("v310.1", row.ToVersion);
    }
}
