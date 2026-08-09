using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers which dlls "revert all to defaults" would actually touch. This decides both what the
/// confirmation prompt claims and what gets restored, so the two cannot be allowed to disagree.
/// </summary>
public class DllUpdateRunnerTests
{
    static GameAsset Asset(string gameId, GameAssetType assetType)
    {
        return new GameAsset()
        {
            Id = gameId,
            AssetType = assetType,
            Path = $@"C:\game\{assetType}.dll",
            Version = "310.1.0.0",
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
}
