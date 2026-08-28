using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Swapping;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers what pinning one dll in one game changes, everywhere it changes it.
/// </summary>
/// <remarks>
/// The rule is one sentence: no batch moves a pinned dll, and the picker always can. Everything
/// here is that sentence read from a different surface — the update lists, the revert lists, the
/// row's words, the card's words — because a pin that one surface forgot to read is a batch
/// quietly overwriting the version the user rolled back to on purpose.
/// </remarks>
[Collection(ManifestCollection.Name)]
public class DllPinTests
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

    static string BackupAwarePath(GameAssetType assetType)
    {
        var shadowed = DllTypes.All.FirstOrDefault(x => x.BackupAssetType == assetType);

        return shadowed is null
            ? $@"C:\game\{assetType}.dll"
            : DllSwapExecutor.GetBackupPath($@"C:\game\{shadowed.AssetType}.dll");
    }

    static GameDllPin Pin(string gameId, GameAssetType assetType, string reason = "")
    {
        return new GameDllPin()
        {
            GameId = gameId,
            AssetType = assetType,
            Reason = reason,
        };
    }

    [Fact]
    public void APinTakesTheDllOutOfUpdatesButNotOutOfTheTruth()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("pin_1");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.SetDllPins(new[] { Pin(game.ID, GameAssetType.DLSS) });

        // Update all reads OutdatedAssetTypes; the row's sentence reads BehindAssetTypes. The
        // first must forget this dll and the second must not, or "pinned" reads as "current".
        Assert.DoesNotContain(GameAssetType.DLSS, game.OutdatedAssetTypes);
        Assert.Contains(GameAssetType.DLSS, game.BehindAssetTypes);
        Assert.Empty(game.AvailableUpdates);
    }

    [Fact]
    public void ThePinnedRowSaysPinnedNamesTheNewerVersionAndKeepsTheReason()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("pin_2");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.SetDllPins(new[] { Pin(game.ID, GameAssetType.DLSS, "newer versions ghost in this game") });

        var row = UpscalerRowStatus.For(game, GameAssetType.DLSS);

        Assert.Contains("310.1", row.Sentence);
        Assert.Contains("310.7", row.Sentence);
        Assert.Contains("newer versions ghost in this game", row.Sentence);
        Assert.True(row.IsPinned);
        Assert.Equal("\ue718", row.Glyph);
        Assert.Equal(ResourceHelper.GetString("GamePage_Row_Unpin"), row.PinActionText);
    }

    [Fact]
    public void ARowWithNothingInstalledOffersNoPin()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("pin_3");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.RefreshUpdateAvailable();

        Assert.False(UpscalerRowStatus.For(game, GameAssetType.XeSS).IsPinnable);
        Assert.True(UpscalerRowStatus.For(game, GameAssetType.DLSS).IsPinnable);
    }

    [Fact]
    public void RevertRunsLeavePinnedDllsAlone()
    {
        var game = new TestGame("pin_4");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));
        game.GameAssets.Add(Asset(game.ID, DllTypes.ForAssetType(GameAssetType.DLSS)!.BackupAssetType));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS));
        game.GameAssets.Add(Asset(game.ID, DllTypes.ForAssetType(GameAssetType.XeSS)!.BackupAssetType));
        game.SetDllPins(new[] { Pin(game.ID, GameAssetType.DLSS) });

        // The preview builds on the same list, so the claim follows the act for free.
        Assert.Equal(new[] { GameAssetType.XeSS }, DllUpdateRunner.GetRevertableAssetTypes(game));
        var previewRow = Assert.Single(DllUpdateRunner.GetRevertPreview(game));
        Assert.Equal(ResourceHelper.GetString("General_Name_XeSS"), previewRow.EngineName);
    }

    [Fact]
    public void TheCardSpeaksOnlyWhileAPinHoldsSomethingBack()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var behindAndPinned = new TestGame("pin_5");
        behindAndPinned.GameAssets.Add(Asset(behindAndPinned.ID, GameAssetType.DLSS, "310.1.0.0"));
        behindAndPinned.GameAssets.Add(Asset(behindAndPinned.ID, DllTypes.ForAssetType(GameAssetType.DLSS)!.BackupAssetType, "310.1.0.0"));
        behindAndPinned.SetDllPins(new[] { Pin(behindAndPinned.ID, GameAssetType.DLSS) });

        var status = GameRowStatus.For(behindAndPinned);
        Assert.Equal(GameRowState.Pinned, status.State);
        Assert.Contains("DLSS", status.Sentence);
        Assert.Null(status.ActionLabel);

        // Pinned at the newest: the pin changes nothing right now, so the card has nothing to say.
        var currentAndPinned = new TestGame("pin_5b");
        currentAndPinned.GameAssets.Add(Asset(currentAndPinned.ID, GameAssetType.DLSS, "310.7.0.0"));
        currentAndPinned.GameAssets.Add(Asset(currentAndPinned.ID, DllTypes.ForAssetType(GameAssetType.DLSS)!.BackupAssetType, "310.7.0.0"));
        currentAndPinned.SetDllPins(new[] { Pin(currentAndPinned.ID, GameAssetType.DLSS) });

        Assert.Equal(GameRowState.UpToDate, GameRowStatus.For(currentAndPinned).State);
    }

    [Fact]
    public void AnUnpinnedUpdateStillOutranksThePinnedOne()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.XeSS, "2.0.2.0");

        var game = new TestGame("pin_6");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.GameAssets.Add(Asset(game.ID, DllTypes.ForAssetType(GameAssetType.DLSS)!.BackupAssetType, "310.1.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS, "2.0.1.0"));
        game.GameAssets.Add(Asset(game.ID, DllTypes.ForAssetType(GameAssetType.XeSS)!.BackupAssetType, "2.0.1.0"));
        game.SetDllPins(new[] { Pin(game.ID, GameAssetType.DLSS) });

        // The XeSS update is still real and still offered; the card leads with it.
        var status = GameRowStatus.For(game);
        Assert.Equal(GameRowState.HasUpdates, status.State);

        // And the update list holds exactly the unpinned dll.
        Assert.Equal(new[] { GameAssetType.XeSS }, game.OutdatedAssetTypes);
    }

    [Fact]
    public void PinningAgainReplacesTheReasonRatherThanStackingPins()
    {
        var game = new TestGame("pin_7");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS));
        game.SetDllPins(new[] { Pin(game.ID, GameAssetType.DLSS, "first thought") });
        game.SetDllPins(new[] { Pin(game.ID, GameAssetType.DLSS, "better thought") });

        Assert.Single(game.DllPins);
        Assert.Equal("better thought", game.DllPinFor(GameAssetType.DLSS)?.Reason);
    }
}
