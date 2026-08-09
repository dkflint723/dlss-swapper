using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Dlls;

namespace DLSS_Swapper.Data;

/// <summary>
/// One vendor's worth of out of date dlls across the whole library.
/// </summary>
public class VendorUpdateSummary
{
    public required DllVendor Vendor { get; init; }

    /// <summary>Short technology name, such as "XeSS". Matches the per game badge.</summary>
    public required string Label { get; init; }

    /// <summary>How many games are behind on at least one dll from this vendor.</summary>
    public required int GameCount { get; init; }

    /// <summary>How many out of date dlls this vendor accounts for across those games.</summary>
    public required int DllCount { get; init; }
}

/// <summary>
/// What in the library needs attention.
/// </summary>
/// <remarks>
/// <para>
/// A snapshot rather than a live object. Recompute it when games change; do not hold one and
/// mutate it, or it will drift from the per game badges it is meant to summarise.
/// </para>
/// <para>
/// It reads Game.OutdatedAssetTypes rather than recomputing which dlls are behind. That matters:
/// a summary that decided out of date-ness for itself would be a second implementation of the
/// ranking rules, free to disagree with the badges while looking perfectly reasonable. Every
/// version bug in this project so far has come from two things that must agree being kept in step
/// by hand, so this deliberately has no opinion of its own.
/// </para>
/// <para>
/// It carries counts that change a decision and nothing else. Totals like dlls managed or games per
/// launcher were tried and removed: they read as informative, but nobody does anything differently
/// for knowing them.
/// </para>
/// </remarks>
public class LibrarySummary
{
    public required int TotalGames { get; init; }

    /// <summary>Games with at least one out of date dll, counted once each however far behind.</summary>
    public required int GamesWithUpdates { get; init; }

    /// <summary>Every out of date dll across the library, counted individually.</summary>
    public required int OutdatedDllCount { get; init; }

    /// <summary>One entry per vendor with something out of date, ordered for stable display.</summary>
    public required IReadOnlyList<VendorUpdateSummary> ByVendor { get; init; }

    /// <summary>
    /// Games with at least one swappable dll that has no backup beside it.
    /// </summary>
    /// <remarks>
    /// Worth surfacing because a missing backup is only discovered when someone tries to revert,
    /// which is the worst moment to find out. Games are backed up automatically as they are
    /// detected, so a non zero count here means that did not happen for some of them.
    /// </remarks>
    public required int GamesMissingBackups { get; init; }

    public bool HasUpdates => GamesWithUpdates > 0;

    public static LibrarySummary FromGames(IEnumerable<Game> games)
    {
        var gameList = games as IReadOnlyList<Game> ?? games.ToList();

        var gamesWithUpdates = 0;
        var outdatedDllCount = 0;
        var gamesMissingBackups = 0;

        // Games per vendor and dlls per vendor are counted separately because they answer different
        // questions. Three out of date NVIDIA dlls in one game is one game, three dlls.
        var gameCountByVendor = new Dictionary<DllVendor, int>();
        var dllCountByVendor = new Dictionary<DllVendor, int>();

        foreach (var game in gameList)
        {
            if (IsMissingABackup(game))
            {
                gamesMissingBackups += 1;
            }

            var outdatedAssetTypes = game.OutdatedAssetTypes;
            if (outdatedAssetTypes.Count == 0)
            {
                continue;
            }

            // These counts drive a button that offers to update everything it counted, so a game
            // the user has excluded must not be in them. The game is still behind, and its own row
            // still says so; it is simply not part of what the batch will touch.
            if (game.SkipUpdates)
            {
                continue;
            }

            gamesWithUpdates += 1;
            outdatedDllCount += outdatedAssetTypes.Count;

            var vendorsInThisGame = new HashSet<DllVendor>();
            foreach (var assetType in outdatedAssetTypes)
            {
                var vendor = DLLManager.Instance.GetAssetVendor(assetType);

                // Matches the per game badge, which drops unknown vendors rather than showing a
                // badge with nothing meaningful on it.
                if (vendor == DllVendor.Unknown)
                {
                    continue;
                }

                vendorsInThisGame.Add(vendor);
                dllCountByVendor.TryGetValue(vendor, out var dlls);
                dllCountByVendor[vendor] = dlls + 1;
            }

            foreach (var vendor in vendorsInThisGame)
            {
                gameCountByVendor.TryGetValue(vendor, out var count);
                gameCountByVendor[vendor] = count + 1;
            }
        }

        var byVendor = gameCountByVendor
            .OrderBy(x => x.Key)
            .Select(x => new VendorUpdateSummary()
            {
                Vendor = x.Key,
                Label = DLLManager.Instance.GetVendorShortName(x.Key),
                GameCount = x.Value,
                DllCount = dllCountByVendor.TryGetValue(x.Key, out var dlls) ? dlls : 0,
            })
            .ToList();

        return new LibrarySummary()
        {
            TotalGames = gameList.Count,
            GamesWithUpdates = gamesWithUpdates,
            OutdatedDllCount = outdatedDllCount,
            ByVendor = byVendor,
            GamesMissingBackups = gamesMissingBackups,
        };
    }

    /// <summary>
    /// Whether any swappable dll in this game has no backup of its type.
    /// </summary>
    /// <remarks>
    /// Judged per type rather than per file. A game carrying the same dll in two folders needs one
    /// backup of that type to be revertable, so counting per file would report a gap that reverting
    /// does not actually have.
    /// </remarks>
    static bool IsMissingABackup(Game game)
    {
        foreach (var gameAsset in game.GameAssets)
        {
            var definition = DllTypes.ForAssetType(gameAsset.AssetType);
            if (definition is null)
            {
                // Already a backup, or a type we do not manage.
                continue;
            }

            var hasBackup = false;
            foreach (var candidate in game.GameAssets)
            {
                if (candidate.AssetType == definition.BackupAssetType)
                {
                    hasBackup = true;
                    break;
                }
            }

            if (hasBackup == false)
            {
                return true;
            }
        }

        return false;
    }
}
