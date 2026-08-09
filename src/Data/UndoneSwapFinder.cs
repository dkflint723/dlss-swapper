using System;
using System.Collections.Generic;
using System.Linq;

namespace DLSS_Swapper.Data;

/// <summary>
/// A dll that was swapped and has since been changed by something other than this app.
/// </summary>
public class UndoneSwap
{
    public required string GameId { get; init; }

    public required GameAssetType AssetType { get; init; }

    /// <summary>When the dll was found to have changed.</summary>
    public required DateTime ChangedAt { get; init; }
}

/// <summary>
/// Finds swaps that no longer hold.
/// </summary>
/// <remarks>
/// <para>
/// This is the failure this app cannot otherwise tell you about. A game patch overwrites the dll
/// you swapped in, putting the version the developer shipped back, and nothing announces it. Worse,
/// the scan that notices deletes the backup, because the game's new stock dll is now the thing
/// worth reverting to. So the swap and the way back both disappear quietly, and today the only way
/// to find out is to open that game and read its history.
/// </para>
/// <para>
/// Reads history rather than comparing versions, because the installed dll after a patch may well
/// be newer than what was swapped in. "Behind the latest" and "not what you chose" are different
/// questions and only history can answer the second.
/// </para>
/// </remarks>
public static class UndoneSwapFinder
{
    /// <summary>
    /// Finds every dll whose swap was undone, newest first.
    /// </summary>
    /// <param name="history">History rows for any number of games.</param>
    public static IReadOnlyList<UndoneSwap> Find(IEnumerable<GameHistory> history)
    {
        var undoneSwaps = new List<UndoneSwap>();

        // Per dll rather than per game, since a game can have one dll patched over while another
        // still holds the version that was swapped in.
        var byAsset = history
            .Where(x => x.AssetType is not null)
            .GroupBy(x => (x.GameId, x.AssetType!.Value));

        foreach (var assetHistory in byAsset)
        {
            var swappedAt = LatestTimeOf(assetHistory, GameHistoryEventType.DLLSwapped);
            if (swappedAt is null)
            {
                // Never swapped, so a dll changing is just the game being updated.
                continue;
            }

            var changedAt = LatestTimeOf(assetHistory, GameHistoryEventType.DLLChangedExternally);
            if (changedAt is null || changedAt <= swappedAt)
            {
                continue;
            }

            // A reset after the swap means the stock dll was put back deliberately, so there was no
            // swap left standing for the external change to undo.
            var resetAt = LatestTimeOf(assetHistory, GameHistoryEventType.DLLReset);
            if (resetAt is not null && resetAt > swappedAt)
            {
                continue;
            }

            undoneSwaps.Add(new UndoneSwap()
            {
                GameId = assetHistory.Key.GameId,
                AssetType = assetHistory.Key.Item2,
                ChangedAt = changedAt.Value,
            });
        }

        return undoneSwaps
            .OrderByDescending(x => x.ChangedAt)
            .ToList();
    }

    static DateTime? LatestTimeOf(IEnumerable<GameHistory> history, GameHistoryEventType eventType)
    {
        DateTime? latest = null;
        foreach (var row in history)
        {
            if (row.EventType == eventType && (latest is null || row.EventTime > latest))
            {
                latest = row.EventTime;
            }
        }

        return latest;
    }
}

