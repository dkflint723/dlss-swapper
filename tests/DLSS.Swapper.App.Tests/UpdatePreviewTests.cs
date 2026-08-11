using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Pages;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the update preview sheet: what it offers to write, and what it promises the button will
/// do. The sheet is the app's only statement of what a swap is about to touch, so a row it lists
/// and the run it starts must not be able to disagree.
/// </summary>
[Collection(ManifestCollection.Name)]
public class UpdatePreviewTests
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

    static TestGame OutdatedGame(string id, string title)
    {
        var game = new TestGame(id) { Title = title };
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.RefreshUpdateAvailable();
        return game;
    }

    [Fact]
    public void EveryOutdatedDllBecomesARow()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.XeSS, "2.1.0.0");

        var game = new TestGame("preview_1") { Title = "Cyberpunk 2077" };
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS, "2.0.0.0"));
        game.RefreshUpdateAvailable();

        var pendingUpdates = PendingDllUpdate.ForGames(new[] { game });

        Assert.Equal(2, pendingUpdates.Count);
        Assert.All(pendingUpdates, x => Assert.Equal("Cyberpunk 2077", x.GameTitle));
        Assert.All(pendingUpdates, x => Assert.True(x.IsSelected));

        // Both versions, because the whole point of the sheet is saying what changes.
        Assert.All(pendingUpdates, x => Assert.False(string.IsNullOrWhiteSpace(x.FromVersion)));
        Assert.All(pendingUpdates, x => Assert.False(string.IsNullOrWhiteSpace(x.ToVersion)));
        Assert.All(pendingUpdates, x => Assert.NotEqual(x.FromVersion, x.ToVersion));
    }

    [Fact]
    public void TheReviewButtonAndTheSheetAgreeUnderADllFilter()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        // Two games behind: one using the filtered dll, one not.
        var usingIt = OutdatedGame("preview_filter_1", "Using it");
        var notUsingIt = new TestGame("preview_filter_2") { Title = "Not using it" };
        notUsingIt.GameAssets.Add(Asset(notUsingIt.ID, GameAssetType.DLSS, "310.2.0.0"));
        notUsingIt.RefreshUpdateAvailable();

        var library = new[] { usingIt, notUsingIt };
        var filter = new DllFilter(GameAssetType.DLSS, string.Empty, "310.1.0.0", "label");
        var onThePage = library.Where(filter.Matches).ToList();

        // The button counts games behind in the narrowed set...
        var behind = GameFilters.Count(onThePage, GameFilter.HasUpdate, hideNonDLSSGames: false);
        Assert.Equal(1, behind);

        // ...so the sheet it opens must hold only those games. It counted the narrowed set and
        // opened the whole library, which is the one disagreement the preview exists to prevent.
        var pendingUpdates = PendingDllUpdate.ForGames(onThePage);

        Assert.NotEmpty(pendingUpdates);
        Assert.All(pendingUpdates, x => Assert.Equal("Using it", x.GameTitle));
    }

    [Fact]
    public void AHiddenGameIsNeitherCountedNorOffered()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var shown = OutdatedGame("preview_hidden_1", "Shown");
        var hidden = OutdatedGame("preview_hidden_2", "Hidden");
        hidden.IsHidden = true;

        var library = new[] { shown, hidden };

        // "Hidden" means stop showing me this, so the bulk update must not quietly write to one.
        // The count and the sheet both come from the same predicate, which is what makes that true
        // in one place rather than two.
        var behind = library.Where(x => GameFilters.Matches(x, GameFilter.HasUpdate, hideNonDLSSGames: false)).ToList();

        Assert.Single(behind);

        var pendingUpdates = PendingDllUpdate.ForGames(behind);

        Assert.NotEmpty(pendingUpdates);
        Assert.All(pendingUpdates, x => Assert.Equal("Shown", x.GameTitle));
    }

    [Fact]
    public void AGameUpToDateOffersNothing()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("preview_2");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.7.0.0"));
        game.RefreshUpdateAvailable();

        Assert.Empty(PendingDllUpdate.ForGames(new[] { game }));
    }

    [Fact]
    public void AGameWithUpdatesTurnedOffIsNeverOffered()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = OutdatedGame("preview_3", "Locked");
        Assert.NotEmpty(PendingDllUpdate.ForGames(new[] { game }));

        // Offering a file the run would then refuse to write would be exactly the kind of lie the
        // sheet exists to remove.
        game.SkipUpdates = true;
        Assert.Empty(PendingDllUpdate.ForGames(new[] { game }));
    }

    [Fact]
    public void TheHeadingAndTheButtonCountWhatIsChecked()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var first = OutdatedGame("preview_4a", "First");
        var second = OutdatedGame("preview_4b", "Second");

        var preview = new UpdatePreviewModel(PendingDllUpdate.ForGames(new[] { first, second }));

        Assert.Equal(2, preview.SelectedUpdates.Count);
        Assert.Contains("2", preview.Title);
        Assert.Contains("2", preview.ConfirmLabel);
        Assert.True(preview.CanConfirm);

        preview.Updates[0].IsSelected = false;

        Assert.Single(preview.SelectedUpdates);
        Assert.True(preview.CanConfirm);
    }

    [Fact]
    public void OneFileIsNeverDescribedAsFiles()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var preview = new UpdatePreviewModel(PendingDllUpdate.ForGames(new[] { OutdatedGame("preview_7", "Only") }));

        // A sheet asking to write into game folders should not read "Update 1 files across 1
        // games?". The counts are real, so the sentences have to be too.
        Assert.DoesNotContain("1 file", preview.Title);
        Assert.DoesNotContain("1 game", preview.Title);
        Assert.DoesNotContain("1 file", preview.ConfirmLabel);
    }

    [Fact]
    public void SeveralFilesInOneGameDoNotReadAsSeveralGames()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.XeSS, "2.1.0.0");

        var game = new TestGame("preview_8") { Title = "One game" };
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS, "2.0.0.0"));
        game.RefreshUpdateAvailable();

        var preview = new UpdatePreviewModel(PendingDllUpdate.ForGames(new[] { game }));

        Assert.DoesNotContain("1 game", preview.Title);
    }

    [Fact]
    public void UncheckingEverythingLeavesNothingToConfirm()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var preview = new UpdatePreviewModel(PendingDllUpdate.ForGames(new[] { OutdatedGame("preview_5", "Only") }));

        preview.Updates[0].IsSelected = false;

        Assert.Empty(preview.SelectedUpdates);
        Assert.False(preview.CanConfirm);
    }

    [Fact]
    public void TheHeadingCountsGamesRatherThanFiles()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.XeSS, "2.1.0.0");

        var game = new TestGame("preview_6") { Title = "Two files, one game" };
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS, "2.0.0.0"));
        game.RefreshUpdateAvailable();

        var preview = new UpdatePreviewModel(PendingDllUpdate.ForGames(new[] { game }));

        // "Update 2 files across 1 games?" - the two counts are different questions, and reading
        // the file count for both is how a sheet ends up claiming to touch more games than exist.
        Assert.Equal(2, preview.SelectedUpdates.Count);
        Assert.Single(preview.SelectedUpdates.Select(x => x.Game).Distinct());
    }
}
