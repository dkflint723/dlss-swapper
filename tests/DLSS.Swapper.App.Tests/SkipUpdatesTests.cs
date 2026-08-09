using System.Collections.Generic;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers marking a game so bulk updates leave it alone.
/// </summary>
/// <remarks>
/// For games where a newer dll causes a problem rather than fixes one: anti cheat in multiplayer
/// titles can flag a modified dll and refuse to launch. Getting this wrong writes to a game the
/// user explicitly asked not to touch, which is worse than not having the feature.
/// </remarks>
[Collection(ManifestCollection.Name)]
public class SkipUpdatesTests
{
    static GameAsset Asset(string gameId, GameAssetType assetType, string version)
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

    static TestGame GameWith(string id, params (GameAssetType AssetType, string Version)[] installed)
    {
        var game = new TestGame(id);
        foreach (var dll in installed)
        {
            game.GameAssets.Add(Asset(game.ID, dll.AssetType, dll.Version));
            game.GameAssets.Add(Asset(game.ID, DllTypes.ForAssetType(dll.AssetType)!.BackupAssetType, dll.Version));
        }

        game.RefreshUpdateAvailable();
        return game;
    }

    [Fact]
    public void GamesAreUpdatableByDefault()
    {
        Assert.False(new TestGame("skip_1").SkipUpdates);
    }

    [Fact]
    public void ASkippedGameSaysSoInsteadOfOfferingAnUpdate()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("skip_2", (GameAssetType.DLSS, "310.1.0.0"));
        game.SkipUpdates = true;

        var status = GameRowStatus.For(game);

        Assert.Equal(GameRowState.UpdatesSkipped, status.State);

        // No button, because offering one would contradict the setting the user just made.
        Assert.Null(status.ActionLabel);
    }

    [Fact]
    public void ASkippedGameStillKnowsItIsBehind()
    {
        // The fact is unchanged; only what the app offers to do about it changes. Hiding the fact
        // would make it impossible to review the decision later.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("skip_3", (GameAssetType.DLSS, "310.1.0.0"));
        game.SkipUpdates = true;

        Assert.True(game.UpdateAvailable);
        Assert.NotEmpty(game.OutdatedAssetTypes);
    }

    [Fact]
    public void ASkippedGameIsNotCountedInTheReviewTotals()
    {
        // The count drives a button offering to update everything it counted, so counting a game
        // the batch will skip would promise something it does not do.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var skipped = GameWith("skip_4a", (GameAssetType.DLSS, "310.1.0.0"));
        skipped.SkipUpdates = true;
        var normal = GameWith("skip_4b", (GameAssetType.DLSS, "310.1.0.0"));

        var summary = LibrarySummary.FromGames(new List<Game>() { skipped, normal });

        Assert.Equal(2, summary.TotalGames);
        Assert.Equal(1, summary.GamesWithUpdates);
        Assert.Equal(1, summary.OutdatedDllCount);
    }

    [Fact]
    public void ALibraryWhereEveryOutdatedGameIsSkippedHasNothingToReview()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var skipped = GameWith("skip_5", (GameAssetType.DLSS, "310.1.0.0"));
        skipped.SkipUpdates = true;

        var summary = LibrarySummary.FromGames(new List<Game>() { skipped });

        Assert.False(summary.HasUpdates);
        Assert.Empty(summary.ByVendor);
    }

    [Fact]
    public void ALockedGameCanStillBeOfferedASavedCopy()
    {
        // Saving a copy is not a change to the game, and locking one makes its original more
        // valuable rather than less. When the lock outranked this, a locked game missing its
        // original showed no button and had no route to fixing it from its row.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("skip_10");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.7.0.0"));
        game.SkipUpdates = true;
        game.RefreshUpdateAvailable();

        var status = GameRowStatus.For(game);

        Assert.Equal(GameRowState.NoBackup, status.State);
        Assert.False(string.IsNullOrEmpty(status.ActionLabel));
    }

    [Fact]
    public void ALockedGameWithItsCopySavedSaysItIsLocked()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("skip_11", (GameAssetType.DLSS, "310.1.0.0"));
        game.SkipUpdates = true;

        Assert.Equal(GameRowState.UpdatesSkipped, GameRowStatus.For(game).State);
    }

    [Fact]
    public void SkippingDoesNotHideAMissingBackup()
    {
        // Not updating a game is a reason to care more about having a copy of its original, not
        // less, so the backup warning has to survive the setting.
        using var manifest = new ManifestScope();

        var game = new TestGame("skip_6");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.7.0.0"));
        game.SkipUpdates = true;
        game.RefreshUpdateAvailable();

        var summary = LibrarySummary.FromGames(new List<Game>() { game });

        Assert.Equal(1, summary.GamesMissingBackups);
    }

    [Fact]
    public void TurningUpdatesBackOnRestoresTheOffer()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("skip_7", (GameAssetType.DLSS, "310.1.0.0"));
        game.SkipUpdates = true;
        Assert.Equal(GameRowState.UpdatesSkipped, GameRowStatus.For(game).State);

        game.SkipUpdates = false;

        Assert.Equal(GameRowState.HasUpdates, GameRowStatus.For(game).State);
    }

    [Fact]
    public void BeingWrittenToStillOutranksTheSetting()
    {
        // If a swap is somehow already in flight, the row must describe that rather than claiming
        // nothing is happening.
        using var manifest = new ManifestScope();

        var game = new TestGame("skip_8");
        game.SkipUpdates = true;
        game.Processing = true;

        Assert.Equal(GameRowState.Swapping, GameRowStatus.For(game).State);
    }

    [Fact]
    public void TheSentenceIsReadableText()
    {
        using var manifest = new ManifestScope();

        var game = new TestGame("skip_9");
        game.SkipUpdates = true;

        var status = GameRowStatus.For(game);

        Assert.DoesNotContain("LangResourceError", status.Sentence);
        Assert.False(string.IsNullOrWhiteSpace(status.Sentence));
    }
}
