using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers splitting a game's dll types into what it has and what it does not.
/// </summary>
/// <remarks>
/// The game page showed all nine types whatever the game shipped, so most of it read "Not found".
/// Getting this split wrong either hides a dll the user could swap or claims one exists that does
/// not, and the second is worse: they would go looking for a control that never appears.
/// </remarks>
[Collection(ManifestCollection.Name)]
public class GameEnginesTests
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
    public void AGameWithNoDllsHasNothingPresent()
    {
        using var manifest = new ManifestScope();

        var split = GameEngines.Split(new TestGame("engines_1"));

        Assert.Empty(split.Present);
        Assert.Equal(DllTypes.All.Length, split.Absent.Count);
    }

    [Fact]
    public void OnlyTheTypesTheGameHasArePresent()
    {
        using var manifest = new ManifestScope();

        var game = new TestGame("engines_2");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS));

        var split = GameEngines.Split(game);

        Assert.Equal(new[] { GameAssetType.DLSS, GameAssetType.XeSS }.OrderBy(x => x), split.Present.OrderBy(x => x));
        Assert.DoesNotContain(GameAssetType.DLSS, split.Absent);
    }

    [Fact]
    public void PresentAndAbsentTogetherCoverEveryTypeExactlyOnce()
    {
        // The property that matters: nothing is dropped and nothing is listed twice, whatever the
        // registry grows to.
        using var manifest = new ManifestScope();

        var game = new TestGame("engines_3");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_G));

        var split = GameEngines.Split(game);

        Assert.Equal(DllTypes.All.Length, split.Present.Count + split.Absent.Count);
        Assert.Empty(split.Present.Intersect(split.Absent));
    }

    [Fact]
    public void ABackupDoesNotMakeItsTypePresent()
    {
        // A saved original is a copy of a dll, not a dll the game ships. Counting it would list a
        // type the game may no longer have.
        using var manifest = new ManifestScope();

        var game = new TestGame("engines_4");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP));

        Assert.Empty(GameEngines.Split(game).Present);
    }

    [Fact]
    public void TheSameDllInTwoFoldersIsOneEntry()
    {
        using var manifest = new ManifestScope();

        var game = new TestGame("engines_5");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));

        Assert.Single(GameEngines.Split(game).Present);
    }

    [Fact]
    public void TypesAreListedInRegistryOrderNotDiscoveryOrder()
    {
        // Two games with the same dlls should read the same way, whatever order the scan found them.
        using var manifest = new ManifestScope();

        var game = new TestGame("engines_6");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));

        var registryOrder = DllTypes.All
            .Select(x => x.AssetType)
            .Where(x => x == GameAssetType.DLSS || x == GameAssetType.XeSS)
            .ToList();

        Assert.Equal(registryOrder, GameEngines.Split(game).Present);
    }

    [Fact]
    public void TheAbsentSummaryNamesThemAndCountsThem()
    {
        using var manifest = new ManifestScope();

        var game = new TestGame("engines_7");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));

        var split = GameEngines.Split(game);

        Assert.DoesNotContain("LangResourceError", split.AbsentSummary);
        Assert.Contains(split.Absent.Count.ToString(), split.AbsentSummary);
    }

    [Fact]
    public void AGameWithEveryTypeHasNoAbsentSummary()
    {
        // Nothing to say, so the line is empty rather than reading "0 upscalers not in this game".
        using var manifest = new ManifestScope();

        var game = new TestGame("engines_8");
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            game.GameAssets.Add(Asset(game.ID, dllTypeDefinition.AssetType));
        }

        var split = GameEngines.Split(game);

        Assert.Empty(split.Absent);
        Assert.Equal(string.Empty, split.AbsentSummary);
    }
}
