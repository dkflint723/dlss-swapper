using DLSS_Swapper.Dlls;
using System.Linq;

namespace DLSS_Swapper.Data;

/// <summary>Which subset of the library the games page is showing.</summary>
public enum GameFilter
{
    All,
    HasUpdate,
    MissingBackup,
    Hidden,
}

/// <summary>
/// Decides whether a game belongs in the current filter.
/// </summary>
/// <remarks>
/// A separate rule from the counts beside each tab, but they have to agree: a tab reading "3" that
/// shows four games is worse than no count at all. Both call this.
/// </remarks>
public static class GameFilters
{
    /// <param name="hideNonDLSSGames">
    /// When true, games with no upscaler in their install folder are excluded. Part of this rule
    /// rather than of the view, so the tab counts and the list it shows cannot disagree about it.
    /// </param>
    public static bool Matches(Game game, GameFilter filter, bool hideNonDLSSGames)
    {
        if (hideNonDLSSGames && game.HasSwappableItems == false)
        {
            return false;
        }

        // Here rather than in the view's predicate, which is where it used to live and nowhere
        // else: "All games" counted hidden games and then did not show them. Steam and Xbox mark
        // their own non-game entries hidden on sight, so that gap was widest on the libraries most
        // people have.
        if (filter != GameFilter.Hidden && game.IsHidden == true)
        {
            return false;
        }

        return filter switch
        {
            GameFilter.HasUpdate => HasUpdate(game),
            GameFilter.MissingBackup => IsMissingABackup(game),
            GameFilter.Hidden => game.IsHidden == true,
            _ => true,
        };
    }

    /// <summary>
    /// Games this filter would show, so a tab's count and its contents come from one rule.
    /// </summary>
    public static int Count(System.Collections.Generic.IEnumerable<Game> games, GameFilter filter, bool hideNonDLSSGames)
    {
        return games.Count(x => Matches(x, filter, hideNonDLSSGames));
    }

    /// <summary>
    /// Behind on something the app would actually offer to update.
    /// </summary>
    /// <remarks>
    /// A game the user marked as leave alone is excluded even though it is behind, because the tab
    /// sits next to a button that offers to update everything in it.
    /// </remarks>
    static bool HasUpdate(Game game)
    {
        return game.SkipUpdates == false && game.OutdatedAssetTypes.Count > 0;
    }

    /// <summary>Any swappable dll with no copy of its original beside it.</summary>
    /// <summary>
    /// Whether any swappable dll in this game has no backup of its type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Public because the sidebar's "Fix the other 3" counts the same games this filter shows, and
    /// pressing it opens this filter. They each had their own copy of this loop, which is two
    /// implementations of one rule and a count that was free to disagree with the list it opened.
    /// </para>
    /// <para>
    /// Judged per type rather than per file. A game carrying the same dll in two folders needs one
    /// backup of that type to be revertable, so counting per file would report a gap that reverting
    /// does not actually have.
    /// </para>
    /// </remarks>
    public static bool IsMissingABackup(Game game)
    {
        // Game.HasSavedOriginal, not a copy of its loop. This asked whether any dll of the same
        // TYPE had a backup, so a game shipping one dll in two folders reported itself protected on
        // the strength of a copy of the other location.
        foreach (var gameAsset in game.GameAssets)
        {
            if (game.HasSavedOriginal(gameAsset) == false)
            {
                return true;
            }
        }

        return false;
    }
}
