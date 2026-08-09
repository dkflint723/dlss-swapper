using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>Why the games list has nothing in it.</summary>
public enum GamesEmptyStateKind
{
    /// <summary>It has something in it.</summary>
    None,

    /// <summary>No games have ever been found, so nothing has been scanned yet.</summary>
    FirstRun,

    /// <summary>Games were found, but none of them ship an upscaler to swap.</summary>
    NoUpscalerGames,

    /// <summary>A search matched nothing.</summary>
    NoSearchResults,
}

/// <summary>
/// What to say when the games list is empty.
/// </summary>
/// <remarks>
/// An empty list has three different causes and they need three different answers. Saying nothing
/// at all -- which is what a blank content area does -- reads as a broken app in every one of them,
/// and the most likely cause on first launch is simply that nobody has asked it to look yet.
/// </remarks>
public class GamesEmptyState
{
    public required GamesEmptyStateKind Kind { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>The suggested way out, or empty when there is not one.</summary>
    public required string PrimaryLabel { get; init; }

    public required string SecondaryLabel { get; init; }

    /// <summary>A quieter line under the buttons. Empty when there is nothing to add.</summary>
    public required string Hint { get; init; }

    GamesEmptyState()
    {
    }

    static readonly GamesEmptyState NotEmpty = new GamesEmptyState()
    {
        Kind = GamesEmptyStateKind.None,
        Title = string.Empty,
        Body = string.Empty,
        PrimaryLabel = string.Empty,
        SecondaryLabel = string.Empty,
        Hint = string.Empty,
    };

    /// <summary>
    /// Works out which of the three the list is showing, if any.
    /// </summary>
    /// <param name="visibleCount">How many games the list is actually showing.</param>
    /// <param name="totalGames">How many games are known about at all.</param>
    /// <param name="searchText">What was typed in the search box.</param>
    /// <param name="isFilteredTab">Whether a tab other than "All games" is selected.</param>
    /// <remarks>
    /// A filtered tab is left alone deliberately. "No games with upscalers" would be a lie on an
    /// empty "Have an update" tab, and that tab already says how many it has beside its own name.
    /// </remarks>
    public static GamesEmptyState For(int visibleCount, int totalGames, string searchText, bool isFilteredTab)
    {
        if (visibleCount > 0)
        {
            return NotEmpty;
        }

        // Checked before anything about the library, because a search that matched nothing says
        // nothing about whether there are games to find.
        if (string.IsNullOrWhiteSpace(searchText) == false)
        {
            return new GamesEmptyState()
            {
                Kind = GamesEmptyStateKind.NoSearchResults,
                Title = ResourceHelper.GetFormattedResourceTemplate("GamesPage_NoSearchResultsTemplate", searchText),
                Body = string.Empty,
                PrimaryLabel = ResourceHelper.GetString("GamesPage_ClearSearch"),
                SecondaryLabel = string.Empty,
                Hint = string.Empty,
            };
        }

        if (isFilteredTab)
        {
            return NotEmpty;
        }

        if (totalGames == 0)
        {
            return new GamesEmptyState()
            {
                Kind = GamesEmptyStateKind.FirstRun,
                Title = ResourceHelper.GetString("FirstRun_Title"),
                Body = ResourceHelper.GetString("FirstRun_Body"),
                PrimaryLabel = ResourceHelper.GetString("FirstRun_Scan"),
                SecondaryLabel = ResourceHelper.GetString("FirstRun_ChooseFolder"),
                Hint = ResourceHelper.GetString("FirstRun_Duration"),
            };
        }

        // Games were found and none of them can be swapped. The second sentence is the caveat the
        // project's own readme states twice and the app never did: this cannot add upscaling to a
        // game that does not already have it.
        return new GamesEmptyState()
        {
            Kind = GamesEmptyStateKind.NoUpscalerGames,
            Title = ResourceHelper.GetString("GamesPage_Empty_Title"),
            Body = ResourceHelper.GetFormattedResourceTemplate("GamesPage_Empty_BodyTemplate", totalGames),
            PrimaryLabel = ResourceHelper.GetFormattedResourceTemplate("GamesPage_Empty_ShowAllTemplate", totalGames),
            SecondaryLabel = ResourceHelper.GetString("GamesPage_AddGame"),
            Hint = string.Empty,
        };
    }
}
