using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>One dll in one game: the unit every batch is made of.</summary>
internal readonly record struct DllWorkItem(Game Game, GameAssetType AssetType);

/// <summary>
/// How far through a run is, and what it is working on.
/// </summary>
/// <remarks>
/// A count as well as a name, because "Updating 3 of 7" is the part that tells someone whether to
/// wait. The old progress report was a single formatted string, which could only ever say what file
/// was being written and never how much was left.
/// </remarks>
internal sealed class DllUpdateProgress
{
    /// <summary>Which file is being worked on, counting from one, so it reads as "3 of 7".</summary>
    public required int CurrentIndex { get; init; }

    public required int TotalCount { get; init; }

    public required string GameTitle { get; init; }

    public required string EngineName { get; init; }

    /// <summary>Reads as "Cyberpunk 2077 — FSR 3.1 DirectX 12".</summary>
    public string Description => $"{GameTitle} — {EngineName}";
}

/// <summary>
/// One dll that was replaced, and what it was replaced with.
/// </summary>
/// <remarks>
/// Recorded as the batch runs, because the version a file was before is only knowable before it is
/// written over. The done strip could say seven files were updated and nothing at all about what
/// they became, which is the one thing worth checking before deciding to keep it.
///
/// Also the row a revert preview is made of, so "what this will do" and "what this did" format a
/// version change one way. See <see cref="DllUpdateRunner.GetRevertPreview"/>.
/// </remarks>
internal sealed class DllChange
{
    public required string GameTitle { get; init; }

    public required string EngineName { get; init; }

    public required string FromVersion { get; init; }

    public required string ToVersion { get; init; }

    /// <summary>Reads as "3.7.20 → 310.7", or just the new version when there was nothing before.</summary>
    public string VersionChange => string.IsNullOrEmpty(FromVersion)
        ? ToVersion
        : ResourceHelper.GetFormattedResourceTemplate("Update_VersionChangeTemplate", FromVersion, ToVersion);

    /// <summary>Reads as "Cyberpunk 2077 — DLSS".</summary>
    public string Description => $"{GameTitle} — {EngineName}";
}

/// <summary>
/// What an update run did.
/// </summary>
internal sealed class DllUpdateResult
{
    /// <summary>
    /// What each written file was before and after, in the order it was written.
    /// </summary>
    /// <remarks>
    /// Alongside <see cref="Succeeded"/> rather than derived from it: an item says which dll in
    /// which game, and by the time anyone reads it the file on disk is already the new one.
    /// </remarks>
    public List<DllChange> Changes { get; } = new List<DllChange>();

    /// <summary>
    /// Exactly which dlls were written.
    /// </summary>
    /// <remarks>
    /// Kept rather than counted, because undoing a batch means putting these back and nothing else.
    /// A count cannot be reversed.
    /// </remarks>
    public List<DllWorkItem> Succeeded { get; } = new List<DllWorkItem>();

    /// <summary>Dlls that could not be updated, one line each, ready to show.</summary>
    public List<string> Failures { get; } = new List<string>();

    /// <summary>Dlls swapped to a newer version.</summary>
    public int Swapped => Succeeded.Count;

    /// <summary>Games that had at least one dll swapped.</summary>
    public int GamesUpdated => Succeeded.Select(x => x.Game).Distinct().Count();
}

/// <summary>
/// Swaps every out of date dll in a game, or across many games, to the newest available.
/// </summary>
/// <remarks>
/// Each swap is the same transactional operation the dll picker performs, so a failure leaves that
/// game as it was. A run keeps going after a failure rather than stopping part way through a batch,
/// and reports what did not work at the end.
/// </remarks>
internal static class DllUpdateRunner
{
    /// <summary>
    /// Updates one game's out of date dlls.
    /// </summary>
    internal static Task<DllUpdateResult> UpdateGameAsync(Game game, IProgress<DllUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return UpdateGamesAsync(new List<Game>() { game }, progress, cancellationToken);
    }

    /// <summary>
    /// Updates every out of date dll across the given games.
    /// </summary>
    internal static Task<DllUpdateResult> UpdateGamesAsync(IReadOnlyList<Game> games, IProgress<DllUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        // Copied because a swap refreshes each game's list underneath us.
        var items = games
            .SelectMany(game => game.OutdatedAssetTypes.Select(assetType => new DllWorkItem(game, assetType)))
            .ToList();

        return RunAsync(items, SwapOneAsync, progress, cancellationToken);
    }

    /// <summary>
    /// Updates exactly the files the preview sheet was left holding.
    /// </summary>
    internal static Task<DllUpdateResult> UpdateSelectedAsync(IReadOnlyList<PendingDllUpdate> updates, IProgress<DllUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var items = updates.Select(x => new DllWorkItem(x.Game, x.AssetType)).ToList();
        return RunAsync(items, SwapOneAsync, progress, cancellationToken);
    }

    /// <summary>
    /// Restores every dll with a backup across the given games.
    /// </summary>
    internal static Task<DllUpdateResult> RevertGamesAsync(IReadOnlyList<Game> games, IProgress<DllUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var items = games
            .SelectMany(game => GetRevertableAssetTypes(game).Select(assetType => new DllWorkItem(game, assetType)))
            .ToList();

        return RunAsync(items, ResetOneAsync, progress, cancellationToken);
    }

    /// <summary>
    /// Puts back exactly what a batch wrote, newest write first.
    /// </summary>
    /// <remarks>
    /// Reversed so a game that had several dlls replaced unwinds in the order it was written, and
    /// scoped to the batch so undoing an update cannot quietly revert a swap made last week.
    /// </remarks>
    internal static Task<DllUpdateResult> UndoAsync(IReadOnlyList<DllWorkItem> items, IProgress<DllUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return RunAsync(items.Reverse().ToList(), ResetOneAsync, progress, cancellationToken);
    }

    /// <summary>
    /// The version of one dll in one game right now, or empty when the game has no such file.
    /// </summary>
    /// <remarks>
    /// A swap refreshes the game's asset list, so calling this before and after a write gives the
    /// two ends of the change.
    /// </remarks>
    static string VersionOf(DllWorkItem item)
    {
        foreach (var gameAsset in item.Game.GameAssets)
        {
            if (gameAsset.AssetType == item.AssetType)
            {
                // The display form, so "see what changed" says 310.7 like the sheet that offered
                // the change did, rather than 310.7.0.0. Two views of one fact, formatted two
                // ways, reads as two facts.
                return gameAsset.DisplayVersion ?? string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// The dll types in a game that have a backup to go back to.
    /// </summary>
    internal static List<GameAssetType> GetRevertableAssetTypes(Game game)
    {
        var revertableAssetTypes = new List<GameAssetType>();

        foreach (var dllTypeDefinition in DllTypes.All)
        {
            foreach (var gameAsset in game.GameAssets)
            {
                if (gameAsset.AssetType == dllTypeDefinition.BackupAssetType)
                {
                    revertableAssetTypes.Add(dllTypeDefinition.AssetType);
                    break;
                }
            }
        }

        return revertableAssetTypes;
    }

    /// <summary>
    /// The rows a revert confirmation lists: each dll with a saved original, what it is now, and
    /// what it goes back to.
    /// </summary>
    /// <remarks>
    /// Built on <see cref="GetRevertableAssetTypes"/> so the preview and the run read one rule and
    /// cannot disagree about which dlls are meant. Names come straight off the type definition
    /// rather than through <see cref="DLLManager.GetAssetTypeName"/> — the same field, but read
    /// here so a test can check a preview without standing up the whole manager.
    /// </remarks>
    internal static List<DllChange> GetRevertPreview(Game game)
    {
        var preview = new List<DllChange>();

        foreach (var assetType in GetRevertableAssetTypes(game))
        {
            if (DllTypes.ForAssetType(assetType) is not DllTypeDefinition definition)
            {
                continue;
            }

            preview.Add(new DllChange()
            {
                GameTitle = game.Title,
                EngineName = ResourceHelper.GetString(definition.DisplayNameResourceKey),
                FromVersion = game.GameAssets.FirstOrDefault(x => x.AssetType == assetType)?.DisplayName ?? string.Empty,
                ToVersion = game.GameAssets.FirstOrDefault(x => x.AssetType == definition.BackupAssetType)?.DisplayName ?? string.Empty,
            });
        }

        return preview;
    }

    /// <summary>
    /// Works through a flat list of dlls, one at a time, reporting where it has got to.
    /// </summary>
    /// <remarks>
    /// Flat rather than nested per game so the progress count is the count the user was shown. It
    /// is also the one place every batch passes through, so the locked-game rule cannot be
    /// forgotten by a caller that builds its own list.
    ///
    /// Sequential on purpose. Running these in parallel would mean several games writing dlls at
    /// once with no way to tell the user which one failed and why.
    /// </remarks>
    static async Task<DllUpdateResult> RunAsync(
        IReadOnlyList<DllWorkItem> items,
        Func<DllWorkItem, CancellationToken, Task<DllWorkOutcome>> operation,
        IProgress<DllUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = new DllUpdateResult();
        var currentIndex = 0;

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (item.Game.SkipUpdates)
            {
                continue;
            }

            ++currentIndex;

            progress?.Report(new DllUpdateProgress()
            {
                CurrentIndex = currentIndex,
                TotalCount = items.Count,
                GameTitle = item.Game.Title,
                EngineName = DLLManager.Instance.GetAssetTypeName(item.AssetType),
            });

            // Read before the write, because afterwards there is nothing left to read it from.
            var versionBefore = VersionOf(item);

            var outcome = await operation(item, cancellationToken).ConfigureAwait(false);

            if (outcome.Done)
            {
                result.Succeeded.Add(item);

                result.Changes.Add(new DllChange()
                {
                    GameTitle = item.Game.Title,
                    EngineName = DLLManager.Instance.GetAssetTypeName(item.AssetType),
                    FromVersion = versionBefore,
                    ToVersion = VersionOf(item),
                });
            }

            // Reported alongside rather than instead of being done: a reset can restore some
            // locations and not others, and both facts matter.
            if (string.IsNullOrEmpty(outcome.Failure) == false)
            {
                result.Failures.Add(outcome.Failure);
            }
        }

        return result;
    }

    /// <summary>What one dll's operation did, and what to say about it if anything.</summary>
    readonly record struct DllWorkOutcome(bool Done, string? Failure);

    /// <summary>
    /// Swaps one dll to the newest version there is, downloading it first if it is not on disk.
    /// </summary>
    static async Task<DllWorkOutcome> SwapOneAsync(DllWorkItem item, CancellationToken cancellationToken)
    {
        var assetTypeName = DLLManager.Instance.GetAssetTypeName(item.AssetType);

        var latestRecord = DLLManager.Instance.GetLatestRecord(item.AssetType);
        if (latestRecord is null)
        {
            return new DllWorkOutcome(false, null);
        }

        if (latestRecord.LocalRecord?.IsDownloaded == false)
        {
            // Cancellable, so pressing cancel during a large download stops it rather than waiting
            // for it to finish.
            var didDownload = await latestRecord.DownloadAsync(cancellationToken).ConfigureAwait(false);
            if (didDownload.Success == false)
            {
                // A cancelled download is the user's doing, not a failure worth reporting.
                return new DllWorkOutcome(false, didDownload.Cancelled
                    ? null
                    : $"{item.Game.Title} - {assetTypeName}: {didDownload.Message}");
            }
        }

        var didUpdate = await item.Game.UpdateDllAsync(latestRecord).ConfigureAwait(false);
        return didUpdate.Success
            ? new DllWorkOutcome(true, null)
            : new DllWorkOutcome(false, $"{item.Game.Title} - {assetTypeName}: {didUpdate.Message}");
    }

    /// <summary>
    /// Puts one dll back to the copy saved before it was swapped.
    /// </summary>
    static async Task<DllWorkOutcome> ResetOneAsync(DllWorkItem item, CancellationToken cancellationToken)
    {
        var assetTypeName = DLLManager.Instance.GetAssetTypeName(item.AssetType);

        var didReset = await item.Game.ResetDllAsync(item.AssetType).ConfigureAwait(false);
        if (didReset.Success == false)
        {
            return new DllWorkOutcome(false, $"{item.Game.Title} - {assetTypeName}: {didReset.Message}");
        }

        // A reset can succeed having restored only some locations, and says so in its message.
        return new DllWorkOutcome(true, string.IsNullOrEmpty(didReset.Message)
            ? null
            : $"{item.Game.Title} - {assetTypeName}: {didReset.Message}");
    }
}
