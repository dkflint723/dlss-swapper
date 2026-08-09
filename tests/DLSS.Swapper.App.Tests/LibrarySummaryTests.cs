using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the library wide rollup a dashboard reads. These are the numbers shown largest and
/// checked least, so they get the same scrutiny as the swap path.
/// </summary>
[Collection(ManifestCollection.Name)]
public class LibrarySummaryTests
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

    /// <summary>Builds a game already refreshed against whatever the manifest currently says.</summary>
    static TestGame GameWith(string id, params (GameAssetType AssetType, string Version)[] installed)
    {
        var game = new TestGame(id);
        foreach (var dll in installed)
        {
            game.GameAssets.Add(Asset(game.ID, dll.AssetType, dll.Version));
        }

        game.RefreshUpdateAvailable();
        return game;
    }

    [Fact]
    public void AnEmptyLibrarySummarisesToNothing()
    {
        var summary = LibrarySummary.FromGames(new List<Game>());

        Assert.Equal(0, summary.TotalGames);
        Assert.Equal(0, summary.GamesWithUpdates);
        Assert.Equal(0, summary.OutdatedDllCount);
        Assert.Empty(summary.ByVendor);
        Assert.False(summary.HasUpdates);
    }

    [Fact]
    public void AFullyUpToDateLibraryReportsNoUpdates()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var games = new List<Game>()
        {
            GameWith("lib_1a", (GameAssetType.DLSS, "310.7.0.0")),
            GameWith("lib_1b", (GameAssetType.DLSS, "310.7.0.0")),
        };

        var summary = LibrarySummary.FromGames(games);

        Assert.Equal(2, summary.TotalGames);
        Assert.Equal(0, summary.GamesWithUpdates);
        Assert.False(summary.HasUpdates);
    }

    [Fact]
    public void GamesWithoutDllsStillCountTowardTheTotal()
    {
        // A game with nothing swappable is still in the library. "3 of 40" has to mean 40 games,
        // not 40 games that happen to have dlls, or the denominator drifts from what is on screen.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var games = new List<Game>()
        {
            GameWith("lib_2a", (GameAssetType.DLSS, "310.1.0.0")),
            GameWith("lib_2b"),
        };

        var summary = LibrarySummary.FromGames(games);

        Assert.Equal(2, summary.TotalGames);
        Assert.Equal(1, summary.GamesWithUpdates);
    }

    [Fact]
    public void AGameBehindOnSeveralDllsCountsOnceButItsDllsCountSeparately()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.DLSS_G, "310.7.0.0");
        manifest.Add(GameAssetType.DLSS_D, "310.7.0.0");

        var game = GameWith("lib_3",
            (GameAssetType.DLSS, "310.1.0.0"),
            (GameAssetType.DLSS_G, "310.1.0.0"),
            (GameAssetType.DLSS_D, "310.1.0.0"));

        var summary = LibrarySummary.FromGames(new List<Game>() { game });

        Assert.Equal(1, summary.GamesWithUpdates);
        Assert.Equal(3, summary.OutdatedDllCount);

        var vendor = Assert.Single(summary.ByVendor);
        Assert.Equal(DllVendor.Nvidia, vendor.Vendor);
        Assert.Equal(1, vendor.GameCount);
        Assert.Equal(3, vendor.DllCount);
    }

    [Fact]
    public void AGameBehindOnTwoVendorsCountsOnceUnderEach()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.XeSS, "2.1.0.0");

        var game = GameWith("lib_4",
            (GameAssetType.DLSS, "310.1.0.0"),
            (GameAssetType.XeSS, "2.0.0.0"));

        var summary = LibrarySummary.FromGames(new List<Game>() { game });

        // One game, but it belongs in both vendor buckets, so the vendor counts deliberately sum to
        // more than GamesWithUpdates. They answer different questions.
        Assert.Equal(1, summary.GamesWithUpdates);
        Assert.Equal(2, summary.ByVendor.Count);
        Assert.All(summary.ByVendor, x => Assert.Equal(1, x.GameCount));
    }

    [Fact]
    public void CountsAccumulateAcrossGames()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.DLSS_G, "310.7.0.0");

        var games = new List<Game>()
        {
            GameWith("lib_5a", (GameAssetType.DLSS, "310.1.0.0")),
            GameWith("lib_5b", (GameAssetType.DLSS, "310.1.0.0"), (GameAssetType.DLSS_G, "310.1.0.0")),
            GameWith("lib_5c", (GameAssetType.DLSS, "310.7.0.0")),
        };

        var summary = LibrarySummary.FromGames(games);

        Assert.Equal(3, summary.TotalGames);
        Assert.Equal(2, summary.GamesWithUpdates);
        Assert.Equal(3, summary.OutdatedDllCount);

        var nvidia = Assert.Single(summary.ByVendor);
        Assert.Equal(2, nvidia.GameCount);
        Assert.Equal(3, nvidia.DllCount);
    }

    [Fact]
    public void VendorsAreOrderedTheSameWayEveryTime()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.XeSS, "2.1.0.0");
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        // Added Intel first, NVIDIA second, so insertion order would put Intel on the left. The
        // dashboard must not reshuffle between refreshes.
        var games = new List<Game>()
        {
            GameWith("lib_6a", (GameAssetType.XeSS, "2.0.0.0")),
            GameWith("lib_6b", (GameAssetType.DLSS, "310.1.0.0")),
        };

        var summary = LibrarySummary.FromGames(games);

        Assert.Equal(
            new[] { DllVendor.Nvidia, DllVendor.Intel },
            summary.ByVendor.Select(x => x.Vendor));
    }

    [Fact]
    public void VendorLabelsAreReadableText()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("lib_7", (GameAssetType.DLSS, "310.1.0.0"));

        var summary = LibrarySummary.FromGames(new List<Game>() { game });

        // The dashboard has to read as text, not colour. It resolves through the resource map, which
        // reports failure as a sentinel string rather than throwing.
        var vendor = Assert.Single(summary.ByVendor);
        Assert.False(string.IsNullOrWhiteSpace(vendor.Label));
        Assert.DoesNotContain("LangResourceError", vendor.Label);
    }

    [Fact]
    public void TheSummaryAgreesWithThePerGameBadges()
    {
        // The property that matters most. Every version bug in this project came from two things
        // that must agree drifting apart, and a dashboard is a second place the same answer gets
        // displayed. If this ever fails, the headline number and the badges are telling the user
        // different things.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.XeSS, "2.1.0.0");

        var games = new List<Game>()
        {
            GameWith("lib_8a", (GameAssetType.DLSS, "310.1.0.0")),
            GameWith("lib_8b", (GameAssetType.DLSS, "310.7.0.0")),
            GameWith("lib_8c", (GameAssetType.XeSS, "2.0.0.0"), (GameAssetType.DLSS, "310.1.0.0")),
            GameWith("lib_8d"),
        };

        var summary = LibrarySummary.FromGames(games);

        var gamesShowingABadge = games.Count(x => x.UpdateAvailable);
        Assert.Equal(gamesShowingABadge, summary.GamesWithUpdates);

        var badgedDllCount = games.Sum(x => x.OutdatedAssetTypes.Count);
        Assert.Equal(badgedDllCount, summary.OutdatedDllCount);
    }

    [Fact]
    public void ASummaryIsASnapshotNotALiveView()
    {
        // Documents the contract: recompute after a swap rather than holding one and expecting it to
        // follow, which is the mistake that left stale badges on screen before.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("lib_9", (GameAssetType.DLSS, "310.1.0.0"));
        var games = new List<Game>() { game };

        var before = LibrarySummary.FromGames(games);
        Assert.Equal(1, before.GamesWithUpdates);

        game.GameAssets[0].Version = "310.7.0.0";
        game.RefreshUpdateAvailable();

        Assert.Equal(1, before.GamesWithUpdates);
        Assert.Equal(0, LibrarySummary.FromGames(games).GamesWithUpdates);
    }
}
