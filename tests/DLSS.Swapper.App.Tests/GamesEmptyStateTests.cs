using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers what the games page says when it has nothing to show.
/// </summary>
/// <remarks>
/// Three causes, three answers, and telling them apart is the whole job: "no games with upscalers"
/// on a fresh install would blame the user's library for the app never having looked.
/// </remarks>
public class GamesEmptyStateTests
{
    [Fact]
    public void AListWithGamesInItSaysNothing()
    {
        var state = GamesEmptyState.For(visibleCount: 12, totalGames: 12, searchText: string.Empty, activeFilter: GameFilter.All, hasDllFilter: false, isScanning: false);

        Assert.Equal(GamesEmptyStateKind.None, state.Kind);
        Assert.Empty(state.Title);
    }

    [Fact]
    public void NothingFoundYetOffersToLook()
    {
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 0, searchText: string.Empty, activeFilter: GameFilter.All, hasDllFilter: false, isScanning: false);

        Assert.Equal(GamesEmptyStateKind.FirstRun, state.Kind);
        Assert.False(string.IsNullOrWhiteSpace(state.Title));
        Assert.False(string.IsNullOrWhiteSpace(state.PrimaryLabel));

        // The reassurance that scanning only reads is the point of this screen, so it is not
        // allowed to be silently empty.
        Assert.False(string.IsNullOrWhiteSpace(state.Body));
        Assert.False(string.IsNullOrWhiteSpace(state.Hint));
    }

    [Fact]
    public void GamesWithNoUpscalersSayWhyAndCountThem()
    {
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 42, searchText: string.Empty, activeFilter: GameFilter.All, hasDllFilter: false, isScanning: false);

        Assert.Equal(GamesEmptyStateKind.NoUpscalerGames, state.Kind);
        Assert.Contains("42", state.Body);
        Assert.Contains("42", state.PrimaryLabel);
    }

    [Fact]
    public void ASearchThatMatchedNothingBlamesTheSearch()
    {
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 42, searchText: "zzz", activeFilter: GameFilter.All, hasDllFilter: false, isScanning: false);

        Assert.Equal(GamesEmptyStateKind.NoSearchResults, state.Kind);
        Assert.Contains("zzz", state.Title);
        Assert.False(string.IsNullOrWhiteSpace(state.PrimaryLabel));
    }

    [Fact]
    public void ASearchOnAnEmptyLibraryStillBlamesTheSearch()
    {
        // Checked before anything about the library: a search that matched nothing says nothing
        // about whether there are games to find, and offering to scan here would ignore the query
        // the user just typed.
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 0, searchText: "zzz", activeFilter: GameFilter.All, hasDllFilter: false, isScanning: false);

        Assert.Equal(GamesEmptyStateKind.NoSearchResults, state.Kind);
    }

    /// <summary>
    /// An empty filter tab states its own truth rather than showing a blank canvas.
    /// </summary>
    /// <remarks>
    /// These used to be left entirely alone, because the generic "no games with upscalers" message
    /// would be a lie on them - which it would. But a blank content area reads as a broken app, so
    /// each tab says the true thing instead: no updates, nothing missing, nothing hidden. No
    /// buttons, because there is nothing to do about good news.
    /// </remarks>
    [Theory]
    [InlineData(GameFilter.HasUpdate)]
    [InlineData(GameFilter.MissingBackup)]
    [InlineData(GameFilter.Hidden)]
    public void AnEmptyFilterTabStatesItsOwnTruth(GameFilter filter)
    {
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 42, searchText: string.Empty, activeFilter: filter, hasDllFilter: false, isScanning: false);

        Assert.Equal(GamesEmptyStateKind.EmptyTab, state.Kind);
        Assert.False(string.IsNullOrWhiteSpace(state.Title));
        Assert.DoesNotContain("LangResourceError", state.Title);
        Assert.Equal(string.Empty, state.PrimaryLabel);
    }

    /// <summary>A dll-narrowed list is left alone: the filter chip already names the dll.</summary>
    [Fact]
    public void ADllFilteredListIsLeftAlone()
    {
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 42, searchText: string.Empty, activeFilter: GameFilter.All, hasDllFilter: true, isScanning: false);

        Assert.Equal(GamesEmptyStateKind.None, state.Kind);
    }

    /// <summary>
    /// While the very first scan runs, the page says so - not "nobody has looked yet".
    /// </summary>
    /// <remarks>
    /// The first-run state used to show during the automatic scan, offering a "Scan my libraries"
    /// button whose press could only start a second copy of the scan already running.
    /// </remarks>
    [Fact]
    public void TheFirstScanSaysItIsScanningRatherThanOfferingToScan()
    {
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 0, searchText: string.Empty, activeFilter: GameFilter.All, hasDllFilter: false, isScanning: true);

        Assert.Equal(GamesEmptyStateKind.Scanning, state.Kind);
        Assert.Equal(string.Empty, state.PrimaryLabel);
        Assert.DoesNotContain("LangResourceError", state.Title);
        Assert.DoesNotContain("LangResourceError", state.Body);
    }

    [Fact]
    public void NoneOfTheCopyIsMissing()
    {
        // The resource map answers a missing key with a sentinel instead of throwing, so a state
        // built from keys that were never added would render as gibberish rather than fail.
        var states = new[]
        {
            GamesEmptyState.For(0, 0, string.Empty, GameFilter.All, false, false),
            GamesEmptyState.For(0, 42, string.Empty, GameFilter.All, false, false),
            GamesEmptyState.For(0, 42, "zzz", GameFilter.All, false, false),
        };

        foreach (var state in states)
        {
            Assert.DoesNotContain("LangResourceError", state.Title);
            Assert.DoesNotContain("LangResourceError", state.Body);
            Assert.DoesNotContain("LangResourceError", state.PrimaryLabel);
            Assert.DoesNotContain("LangResourceError", state.SecondaryLabel);
            Assert.DoesNotContain("LangResourceError", state.Hint);
        }
    }
}
