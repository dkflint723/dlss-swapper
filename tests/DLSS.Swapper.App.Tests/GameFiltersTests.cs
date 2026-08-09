using System.Collections.Generic;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers which games each filter tab shows.
/// </summary>
/// <remarks>
/// Each tab carries a count, and the count and the contents come from this same rule on purpose. A
/// tab reading "3" that opens onto four games is worse than showing no number at all.
/// </remarks>
[Collection(ManifestCollection.Name)]
public class GameFiltersTests
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
    public void AllShowsEverything()
    {
        using var manifest = new ManifestScope();

        var hidden = new TestGame("filter_1");
        hidden.IsHidden = true;

        Assert.True(GameFilters.Matches(new TestGame("filter_1b"), GameFilter.All));
        Assert.True(GameFilters.Matches(hidden, GameFilter.All));
    }

    [Fact]
    public void HasUpdateShowsOnlyGamesThatAreBehind()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var behind = GameWith("filter_2a", (GameAssetType.DLSS, "310.1.0.0"));
        var current = GameWith("filter_2b", (GameAssetType.DLSS, "310.7.0.0"));

        Assert.True(GameFilters.Matches(behind, GameFilter.HasUpdate));
        Assert.False(GameFilters.Matches(current, GameFilter.HasUpdate));
    }

    [Fact]
    public void AGameMarkedToSkipIsNotInHasUpdate()
    {
        // The tab sits next to a button offering to update everything in it, so a game the batch
        // will not touch must not be listed there.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var skipped = GameWith("filter_3", (GameAssetType.DLSS, "310.1.0.0"));
        skipped.SkipUpdates = true;

        Assert.False(GameFilters.Matches(skipped, GameFilter.HasUpdate));
    }

    [Fact]
    public void MissingBackupShowsOnlyGamesWithoutOne()
    {
        using var manifest = new ManifestScope();

        var withBackup = GameWith("filter_4a", (GameAssetType.DLSS, "310.7.0.0"));
        var withoutBackup = new TestGame("filter_4b");
        withoutBackup.GameAssets.Add(Asset(withoutBackup.ID, GameAssetType.DLSS, "310.7.0.0"));

        Assert.False(GameFilters.Matches(withBackup, GameFilter.MissingBackup));
        Assert.True(GameFilters.Matches(withoutBackup, GameFilter.MissingBackup));
    }

    [Fact]
    public void AGameWithNoDllsIsNotMissingABackup()
    {
        using var manifest = new ManifestScope();

        Assert.False(GameFilters.Matches(new TestGame("filter_5"), GameFilter.MissingBackup));
    }

    [Fact]
    public void MissingBackupIgnoresWhetherUpdatesAreSkipped()
    {
        // Not updating a game is a reason to care more about having its original, not less.
        using var manifest = new ManifestScope();

        var game = new TestGame("filter_6");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.7.0.0"));
        game.SkipUpdates = true;

        Assert.True(GameFilters.Matches(game, GameFilter.MissingBackup));
    }

    [Fact]
    public void HiddenShowsOnlyHiddenGames()
    {
        using var manifest = new ManifestScope();

        var hidden = new TestGame("filter_7a");
        hidden.IsHidden = true;

        Assert.True(GameFilters.Matches(hidden, GameFilter.Hidden));
        Assert.False(GameFilters.Matches(new TestGame("filter_7b"), GameFilter.Hidden));
    }

    [Fact]
    public void AGameThatHasNeverBeenSetIsNotHidden()
    {
        // IsHidden is nullable, and null means the user never chose, which is not hidden.
        using var manifest = new ManifestScope();

        var game = new TestGame("filter_8");
        game.IsHidden = null;

        Assert.False(GameFilters.Matches(game, GameFilter.Hidden));
    }

    [Fact]
    public void ACountMatchesWhatTheFilterWouldShow()
    {
        // The property the tabs depend on. If these ever diverge the number lies about the list.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var skipped = GameWith("filter_9d", (GameAssetType.DLSS, "310.1.0.0"));
        skipped.SkipUpdates = true;

        var noBackup = new TestGame("filter_9c");
        noBackup.GameAssets.Add(Asset(noBackup.ID, GameAssetType.DLSS, "310.7.0.0"));

        var games = new List<Game>()
        {
            GameWith("filter_9a", (GameAssetType.DLSS, "310.1.0.0")),
            GameWith("filter_9b", (GameAssetType.DLSS, "310.7.0.0")),
            noBackup,
            skipped,
        };

        foreach (var filter in new[] { GameFilter.All, GameFilter.HasUpdate, GameFilter.MissingBackup, GameFilter.Hidden })
        {
            var shown = games.FindAll(x => GameFilters.Matches(x, filter)).Count;

            Assert.Equal(shown, GameFilters.Count(games, filter));
        }
    }

    [Fact]
    public void TheCountsAreWhatTheLibraryActuallyContains()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var noBackup = new TestGame("filter_10c");
        noBackup.GameAssets.Add(Asset(noBackup.ID, GameAssetType.DLSS, "310.7.0.0"));

        var games = new List<Game>()
        {
            GameWith("filter_10a", (GameAssetType.DLSS, "310.1.0.0")),
            GameWith("filter_10b", (GameAssetType.DLSS, "310.7.0.0")),
            noBackup,
        };

        Assert.Equal(3, GameFilters.Count(games, GameFilter.All));
        Assert.Equal(1, GameFilters.Count(games, GameFilter.HasUpdate));
        Assert.Equal(1, GameFilters.Count(games, GameFilter.MissingBackup));
        Assert.Equal(0, GameFilters.Count(games, GameFilter.Hidden));
    }
}
