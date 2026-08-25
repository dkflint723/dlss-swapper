using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Swapping;
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
            Path = BackupAwarePath(assetType),
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
    public void AllShowsEverythingExceptHiddenGames()
    {
        using var manifest = new ManifestScope();

        var hidden = new TestGame("filter_1");
        hidden.IsHidden = true;

        Assert.True(GameFilters.Matches(new TestGame("filter_1b"), GameFilter.All, hideNonDLSSGames: false));

        // This used to assert the opposite, and the opposite was the bug. The exclusion lived in
        // the view's predicate and nowhere else, so "All games" counted hidden games and then did
        // not show them — most visibly on Steam and Xbox, which mark their own non-game entries
        // hidden the first time they are seen.
        Assert.False(GameFilters.Matches(hidden, GameFilter.All, hideNonDLSSGames: false));
    }

    [Fact]
    public void OnlyTheHiddenTabShowsHiddenGames()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var hidden = GameWith("filter_1c", (GameAssetType.DLSS, "310.1.0.0"));
        hidden.IsHidden = true;

        // Behind on a dll, and missing nothing, but hidden: it belongs to its own tab and to no
        // other. A hidden game counted by "Have an update" is one the update button would offer to
        // write to without ever showing it.
        Assert.True(GameFilters.Matches(hidden, GameFilter.Hidden, hideNonDLSSGames: false));
        Assert.False(GameFilters.Matches(hidden, GameFilter.HasUpdate, hideNonDLSSGames: false));
        Assert.False(GameFilters.Matches(hidden, GameFilter.MissingBackup, hideNonDLSSGames: false));
    }

    [Fact]
    public void HasUpdateShowsOnlyGamesThatAreBehind()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var behind = GameWith("filter_2a", (GameAssetType.DLSS, "310.1.0.0"));
        var current = GameWith("filter_2b", (GameAssetType.DLSS, "310.7.0.0"));

        Assert.True(GameFilters.Matches(behind, GameFilter.HasUpdate, hideNonDLSSGames: false));
        Assert.False(GameFilters.Matches(current, GameFilter.HasUpdate, hideNonDLSSGames: false));
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

        Assert.False(GameFilters.Matches(skipped, GameFilter.HasUpdate, hideNonDLSSGames: false));
    }

    [Fact]
    public void MissingBackupShowsOnlyGamesWithoutOne()
    {
        using var manifest = new ManifestScope();

        var withBackup = GameWith("filter_4a", (GameAssetType.DLSS, "310.7.0.0"));
        var withoutBackup = new TestGame("filter_4b");
        withoutBackup.GameAssets.Add(Asset(withoutBackup.ID, GameAssetType.DLSS, "310.7.0.0"));

        Assert.False(GameFilters.Matches(withBackup, GameFilter.MissingBackup, hideNonDLSSGames: false));
        Assert.True(GameFilters.Matches(withoutBackup, GameFilter.MissingBackup, hideNonDLSSGames: false));
    }

    [Fact]
    public void AGameWithNoDllsIsNotMissingABackup()
    {
        using var manifest = new ManifestScope();

        Assert.False(GameFilters.Matches(new TestGame("filter_5"), GameFilter.MissingBackup, hideNonDLSSGames: false));
    }

    [Fact]
    public void MissingBackupIgnoresWhetherUpdatesAreSkipped()
    {
        // Not updating a game is a reason to care more about having its original, not less.
        using var manifest = new ManifestScope();

        var game = new TestGame("filter_6");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.7.0.0"));
        game.SkipUpdates = true;

        Assert.True(GameFilters.Matches(game, GameFilter.MissingBackup, hideNonDLSSGames: false));
    }

    [Fact]
    public void HiddenShowsOnlyHiddenGames()
    {
        using var manifest = new ManifestScope();

        var hidden = new TestGame("filter_7a");
        hidden.IsHidden = true;

        Assert.True(GameFilters.Matches(hidden, GameFilter.Hidden, hideNonDLSSGames: false));
        Assert.False(GameFilters.Matches(new TestGame("filter_7b"), GameFilter.Hidden, hideNonDLSSGames: false));
    }

    [Fact]
    public void AGameThatHasNeverBeenSetIsNotHidden()
    {
        // IsHidden is nullable, and null means the user never chose, which is not hidden.
        using var manifest = new ManifestScope();

        var game = new TestGame("filter_8");
        game.IsHidden = null;

        Assert.False(GameFilters.Matches(game, GameFilter.Hidden, hideNonDLSSGames: false));
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
            var shown = games.FindAll(x => GameFilters.Matches(x, filter, hideNonDLSSGames: false)).Count;

            Assert.Equal(shown, GameFilters.Count(games, filter, hideNonDLSSGames: false));
        }
    }

    [Fact]
    public void HidingGamesWithoutAnUpscalerRemovesThemFromEveryTab()
    {
        // The setting existed and was read by all three views, but nothing counted with it, so the
        // tabs would have gone on including games the list was hiding.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var withUpscaler = GameWith("filter_11a", (GameAssetType.DLSS, "310.1.0.0"));
        withUpscaler.HasSwappableItems = true;

        var withoutUpscaler = new TestGame("filter_11b");
        withoutUpscaler.HasSwappableItems = false;

        var games = new List<Game>() { withUpscaler, withoutUpscaler };

        Assert.Equal(2, GameFilters.Count(games, GameFilter.All, hideNonDLSSGames: false));
        Assert.Equal(1, GameFilters.Count(games, GameFilter.All, hideNonDLSSGames: true));
    }

    [Fact]
    public void AGameWithAnUpscalerSurvivesTheSetting()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("filter_12", (GameAssetType.DLSS, "310.1.0.0"));
        game.HasSwappableItems = true;

        Assert.True(GameFilters.Matches(game, GameFilter.HasUpdate, hideNonDLSSGames: true));
    }

    [Fact]
    public void TheSettingAppliesToEveryTabNotJustAll()
    {
        // A game with no upscaler cannot be behind or missing a backup, but it can be hidden, and
        // the hidden tab must respect the setting too or it becomes a way to see what was excluded.
        using var manifest = new ManifestScope();

        var hiddenWithoutUpscaler = new TestGame("filter_13");
        hiddenWithoutUpscaler.IsHidden = true;
        hiddenWithoutUpscaler.HasSwappableItems = false;

        var games = new List<Game>() { hiddenWithoutUpscaler };

        Assert.Equal(1, GameFilters.Count(games, GameFilter.Hidden, hideNonDLSSGames: false));
        Assert.Equal(0, GameFilters.Count(games, GameFilter.Hidden, hideNonDLSSGames: true));
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

        Assert.Equal(3, GameFilters.Count(games, GameFilter.All, hideNonDLSSGames: false));
        Assert.Equal(1, GameFilters.Count(games, GameFilter.HasUpdate, hideNonDLSSGames: false));
        Assert.Equal(1, GameFilters.Count(games, GameFilter.MissingBackup, hideNonDLSSGames: false));
        Assert.Equal(0, GameFilters.Count(games, GameFilter.Hidden, hideNonDLSSGames: false));
    }

    /// <summary>Where an asset of this type would actually sit on disk.</summary>
    /// <remarks>
    /// See the note in Asset: a backup is always the dll it shadows plus ".dlsss", so a fixture that
    /// invents a path for it is describing something that cannot exist.
    /// </remarks>
    static string BackupAwarePath(GameAssetType assetType)
    {
        var shadowed = DllTypes.All.FirstOrDefault(x => x.BackupAssetType == assetType);

        return shadowed is null
            ? $@"C:\game\{assetType}.dll"
            : DllSwapExecutor.GetBackupPath($@"C:\game\{shadowed.AssetType}.dll");
    }
}
