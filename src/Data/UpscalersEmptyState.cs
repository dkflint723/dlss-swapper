using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

public enum UpscalersEmptyStateKind
{
    None,
    NoSearchResults,
    NoVersions,
}

/// <summary>
/// What the upscalers list says when it is showing nothing.
/// </summary>
/// <remarks>
/// A sibling of <see cref="GamesEmptyState"/> rather than a reuse of it: that one answers a question
/// about a game library, in game words, and takes two arguments this page has no counterpart for.
/// What is worth copying is its shape, and its rule that the search cause is checked first — a
/// search matching nothing says nothing about whether the engine has any versions.
/// </remarks>
public class UpscalersEmptyState
{
    public required UpscalersEmptyStateKind Kind { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public required string PrimaryLabel { get; init; }

    static readonly UpscalersEmptyState NotEmpty = new UpscalersEmptyState()
    {
        Kind = UpscalersEmptyStateKind.None,
        Title = string.Empty,
        Body = string.Empty,
        PrimaryLabel = string.Empty,
    };

    /// <param name="visibleCount">Rows the list is actually showing.</param>
    /// <param name="engineTotal">Versions this engine has, ignoring the search.</param>
    /// <param name="engineName">The engine whose list is empty, named so the message is about it.</param>
    /// <param name="searchText">What was typed.</param>
    /// <param name="matchesElsewhere">
    /// Matches under the other engines, from the same predicate that produced the counts down the
    /// left — so the dead end can point at where the answer actually is.
    /// </param>
    public static UpscalersEmptyState For(
        int visibleCount,
        int engineTotal,
        string engineName,
        string searchText,
        int matchesElsewhere)
    {
        if (visibleCount > 0)
        {
            return NotEmpty;
        }

        if (string.IsNullOrWhiteSpace(searchText) == false)
        {
            return new UpscalersEmptyState()
            {
                Kind = UpscalersEmptyStateKind.NoSearchResults,
                Title = ResourceHelper.GetFormattedResourceTemplate(
                    "Upscalers_NoSearchResultsTemplate", engineName, searchText),

                // The dead end becomes a next step. The page already knows where the matches are,
                // because the engine column counted them a moment ago.
                Body = matchesElsewhere > 0
                    ? ResourceHelper.GetFormattedResourceTemplate("Upscalers_SearchElsewhereTemplate", matchesElsewhere)
                    : ResourceHelper.GetString("Upscalers_SearchNowhere"),

                PrimaryLabel = ResourceHelper.GetFormattedResourceTemplate(
                    "Upscalers_ShowAllTemplate", engineTotal, engineName),
            };
        }

        // Not hypothetical: the manifest has not necessarily loaded when the page first opens, and
        // that used to render as a silently blank column.
        if (engineTotal == 0)
        {
            return new UpscalersEmptyState()
            {
                Kind = UpscalersEmptyStateKind.NoVersions,
                Title = ResourceHelper.GetFormattedResourceTemplate("Upscalers_NoVersionsTemplate", engineName),
                Body = ResourceHelper.GetString("Upscalers_NoVersionsBody"),
                PrimaryLabel = ResourceHelper.GetString("Upscalers_RefreshList"),
            };
        }

        return NotEmpty;
    }
}
