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

    /// <summary>Nothing yet because the very first scan is still running.</summary>
    Scanning,

    /// <summary>A filter tab with nothing behind it, stating the truth of that tab.</summary>
    EmptyTab,
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
    /// <param name="activeFilter">Which tab is selected.</param>
    /// <param name="hasDllFilter">Whether the list is narrowed to one dll's games.</param>
    /// <param name="isScanning">Whether a library scan is still running.</param>
    /// <remarks>
    /// A dll-filtered list is left alone: the filter chip already names the dll, and "no games use
    /// this here" would only repeat it. An empty filter tab used to be left alone too, on the
    /// grounds that a generic "no games with upscalers" message would be a lie there - which it
    /// would. The blank canvas read as broken instead, so each tab now states its own truth: no
    /// updates, nothing missing, nothing hidden. The original objection was to the lie, not to
    /// saying anything.
    /// </remarks>
    public static GamesEmptyState For(int visibleCount, int totalGames, string searchText, GameFilter activeFilter, bool hasDllFilter, bool isScanning)
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

        if (hasDllFilter)
        {
            return NotEmpty;
        }

        if (activeFilter != GameFilter.All)
        {
            var emptyTabTitleKey = activeFilter switch
            {
                GameFilter.HasUpdate => "GamesPage_EmptyTab_HaveUpdate",
                GameFilter.MissingBackup => "GamesPage_EmptyTab_MissingOriginal",
                _ => "GamesPage_EmptyTab_Hidden",
            };

            return new GamesEmptyState()
            {
                Kind = GamesEmptyStateKind.EmptyTab,
                Title = ResourceHelper.GetString(emptyTabTitleKey),
                Body = string.Empty,
                PrimaryLabel = string.Empty,
                SecondaryLabel = string.Empty,
                Hint = string.Empty,
            };
        }

        if (totalGames == 0 && isScanning)
        {
            // The first scan is still running. Without this the first-run state showed underneath
            // it, offering "Scan my libraries" while the scan it offers was already going - a
            // button whose press could only start a second copy of the work in progress.
            return new GamesEmptyState()
            {
                Kind = GamesEmptyStateKind.Scanning,
                Title = ResourceHelper.GetString("FirstRun_ScanningTitle"),
                Body = ResourceHelper.GetString("FirstRun_ScanningBody"),
                PrimaryLabel = string.Empty,
                SecondaryLabel = string.Empty,
                Hint = string.Empty,
            };
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
