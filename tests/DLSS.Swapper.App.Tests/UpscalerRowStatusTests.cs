using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Swapping;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers what one upscaler row on a game's page says.
/// </summary>
/// <remarks>
/// The row it replaces was a bold type name over a dropdown, and everything else about that
/// upscaler — whether the game was behind, whether an original had been kept, whether the dll had
/// been found in two folders — lived in icon buttons beside it or nowhere at all.
/// </remarks>
[Collection(ManifestCollection.Name)]
public class UpscalerRowStatusTests
{
    static GameAsset Asset(string gameId, GameAssetType assetType, string version = "310.1.0.0")
    {
        return new GameAsset()
        {
            Id = gameId,
            AssetType = assetType,
            Path = BackupAwarePath(assetType),
            Version = version,
            Size = 1024,
            Hash = string.Empty,
        };
    }

    static TestGame GameWith(string id, params GameAsset[] assets)
    {
        var game = new TestGame(id);
        game.GameAssets.AddRange(assets);
        game.RefreshUpdateAvailable();
        return game;
    }

    [Fact]
    public void ARowSaysWhatIsInstalledAndWhetherItIsTheNewest()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var behind = GameWith("row_1", Asset("row_1", GameAssetType.DLSS, "310.1.0.0"));
        var status = UpscalerRowStatus.For(behind, GameAssetType.DLSS);

        // Both versions, because "behind" without saying behind what is a fact you cannot act on.
        Assert.Contains("310.1", status.Sentence);
        Assert.Contains("310.7", status.Sentence);
        Assert.DoesNotContain("LangResourceError", status.Sentence);
        Assert.Equal("DLSS", status.Title);
    }

    [Fact]
    public void ACurrentRowWithItsOriginalKeptCarriesNoGlyph()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith(
            "row_2",
            Asset("row_2", GameAssetType.DLSS, "310.7.0.0"),
            Asset("row_2", DllTypes.ForAssetType(GameAssetType.DLSS)!.BackupAssetType, "310.7.0.0"));

        var status = UpscalerRowStatus.For(game, GameAssetType.DLSS);

        // That is most rows on most games. Marking all of them would make the absence of a mark
        // the exceptional case, which is the opposite of what a mark is for.
        Assert.Empty(status.Glyph);
    }

    [Fact]
    public void ARowWithNoSavedOriginalSaysSo()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("row_3", Asset("row_3", GameAssetType.DLSS, "310.7.0.0"));
        var status = UpscalerRowStatus.For(game, GameAssetType.DLSS);

        Assert.False(string.IsNullOrEmpty(status.Glyph));
        Assert.Contains("310.7", status.Sentence);

        // The one fact that decides whether a swap can be undone, and it was not on the row at all.
        Assert.NotEqual(
            UpscalerRowStatus.For(
                GameWith(
                    "row_3b",
                    Asset("row_3b", GameAssetType.DLSS, "310.7.0.0"),
                    Asset("row_3b", DllTypes.ForAssetType(GameAssetType.DLSS)!.BackupAssetType, "310.7.0.0")),
                GameAssetType.DLSS).Sentence,
            status.Sentence);
    }

    [Fact]
    public void ALeftAloneGameSaysThatRatherThanThatItIsBehind()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("row_4", Asset("row_4", GameAssetType.DLSS, "310.1.0.0"));
        game.SkipUpdates = true;

        var status = UpscalerRowStatus.For(game, GameAssetType.DLSS);

        // Saying it is behind would invite a click the row then refuses, which is the failure the
        // rest of this redesign has been removing.
        Assert.True(status.IsLocked);
        Assert.DoesNotContain("310.7", status.Sentence);
    }

    [Fact]
    public void ARowWithNothingInstalledOffersToChooseSomething()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("row_5", Asset("row_5", GameAssetType.XeSS, "2.0.0.0"));
        var status = UpscalerRowStatus.For(game, GameAssetType.DLSS);

        Assert.False(string.IsNullOrWhiteSpace(status.ActionLabel));
        Assert.DoesNotContain("LangResourceError", status.ActionLabel!);
    }

    [Fact]
    public void TheRowsAndTheNotInThisGameCountCoverEveryUpscaler()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith(
            "row_6",
            Asset("row_6", GameAssetType.DLSS, "310.1.0.0"),
            Asset("row_6", GameAssetType.XeSS, "2.0.0.0"));

        var rows = UpscalerRows.For(game);
        var split = GameEngines.Split(game);

        // The house rule, applied to this page: the rows on screen and the number in the sentence
        // beneath them come from one split. The control they replace worked out for itself whether
        // the game had a given dll, which is the same question asked in a second place.
        Assert.Equal(2, rows.Rows.Count);
        Assert.Equal(DllTypes.All.Length, rows.Rows.Count + split.Absent.Count);
        Assert.False(string.IsNullOrWhiteSpace(rows.AbsentSummary));
    }

    [Fact]
    public void AGameMissingExactlyOneUpscalerIsNotDescribedInThePlural()
    {
        using var manifest = new ManifestScope();

        // Every type but one, so the summary has to say "1 upscaler", not "1 upscalers".
        var game = new TestGame("row_7");
        foreach (var dllTypeDefinition in DllTypes.All.Skip(1))
        {
            game.GameAssets.Add(Asset(game.ID, dllTypeDefinition.AssetType));
        }

        game.RefreshUpdateAvailable();

        var summary = UpscalerRows.For(game).AbsentSummary;

        Assert.DoesNotContain("1 upscalers", summary);
        Assert.DoesNotContain("LangResourceError", summary);
    }

    /// <summary>Where an asset of this type would actually sit on disk.</summary>
    /// <remarks>
    /// See the note in Asset: a backup is always the dll it shadows plus ".dlsss", so a fixture that
    /// invents a path for it is describing something that cannot exist.
    /// </remarks>
    /// <summary>
    /// The page you open to act on a missing original has to agree that it is missing.
    /// </summary>
    /// <remarks>
    /// This row kept a fourth private copy of "has a saved original", asked by asset type rather
    /// than by path - so a game shipping one dll in two folders with a copy beside only one of them
    /// was reported as missing a copy by the list, the row and the sidebar, and as having one by the
    /// game's own page. It reads Game.HasSavedOriginal now, like the other three.
    /// </remarks>
    [Fact]
    public void ARowCoveringTwoLocationsNeedsBothSaved()
    {
        var game = new TestGame("upscaler_two_locations");

        var first = Asset(game.ID, GameAssetType.DLSS, "310.7.0.0");
        var second = new GameAsset()
        {
            Id = game.ID,
            AssetType = GameAssetType.DLSS,
            Path = @"C:\game\Engine\DLSS.dll",
            Version = "310.7.0.0",
            Size = 1024,
            Hash = string.Empty,
        };

        game.GameAssets.Add(first);
        game.GameAssets.Add(second);

        // Only the first location has its original beside it.
        game.GameAssets.Add(new GameAsset()
        {
            Id = game.ID,
            AssetType = GameAssetType.DLSS_BACKUP,
            Path = DllSwapExecutor.GetBackupPath(first.Path),
            Version = "310.7.0.0",
            Size = 1024,
            Hash = string.Empty,
        });

        // The list already says so; the game's own row has to say the same.
        Assert.True(GameFilters.IsMissingABackup(game));
        Assert.Equal(GameRowState.NoBackup, GameRowStatus.For(game).State);

        var row = UpscalerRowStatus.For(game, GameAssetType.DLSS);

        Assert.Contains(ResourceHelper.GetString("GamePage_Row_NoSavedOriginal"), row.Sentence);
    }

    static string BackupAwarePath(GameAssetType assetType)
    {
        var shadowed = DllTypes.All.FirstOrDefault(x => x.BackupAssetType == assetType);

        return shadowed is null
            ? $@"C:\game\{assetType}.dll"
            : DllSwapExecutor.GetBackupPath($@"C:\game\{shadowed.AssetType}.dll");
    }
}
