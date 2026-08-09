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
        var state = GamesEmptyState.For(visibleCount: 12, totalGames: 12, searchText: string.Empty, isFilteredTab: false);

        Assert.Equal(GamesEmptyStateKind.None, state.Kind);
        Assert.Empty(state.Title);
    }

    [Fact]
    public void NothingFoundYetOffersToLook()
    {
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 0, searchText: string.Empty, isFilteredTab: false);

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
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 42, searchText: string.Empty, isFilteredTab: false);

        Assert.Equal(GamesEmptyStateKind.NoUpscalerGames, state.Kind);
        Assert.Contains("42", state.Body);
        Assert.Contains("42", state.PrimaryLabel);
    }

    [Fact]
    public void ASearchThatMatchedNothingBlamesTheSearch()
    {
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 42, searchText: "zzz", isFilteredTab: false);

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
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 0, searchText: "zzz", isFilteredTab: false);

        Assert.Equal(GamesEmptyStateKind.NoSearchResults, state.Kind);
    }

    [Fact]
    public void AnEmptyFilterTabIsLeftAlone()
    {
        // "No games with upscalers yet" would be a lie on an empty "Have an update" tab, and that
        // tab already reports its own count beside its name.
        var state = GamesEmptyState.For(visibleCount: 0, totalGames: 42, searchText: string.Empty, isFilteredTab: true);

        Assert.Equal(GamesEmptyStateKind.None, state.Kind);
    }

    [Fact]
    public void NoneOfTheCopyIsMissing()
    {
        // The resource map answers a missing key with a sentinel instead of throwing, so a state
        // built from keys that were never added would render as gibberish rather than fail.
        var states = new[]
        {
            GamesEmptyState.For(0, 0, string.Empty, false),
            GamesEmptyState.For(0, 42, string.Empty, false),
            GamesEmptyState.For(0, 42, "zzz", false),
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
