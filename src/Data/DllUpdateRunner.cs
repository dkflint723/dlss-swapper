using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>
/// What an update run did.
/// </summary>
internal sealed class DllUpdateResult
{
    /// <summary>Dlls swapped to a newer version.</summary>
    public int Swapped { get; set; }

    /// <summary>Dlls that could not be updated, one line each, ready to show.</summary>
    public List<string> Failures { get; } = new List<string>();

    /// <summary>Games that had at least one dll swapped.</summary>
    public int GamesUpdated { get; set; }

    public void Add(DllUpdateResult other)
    {
        Swapped += other.Swapped;
        Failures.AddRange(other.Failures);

        if (other.Swapped > 0)
        {
            ++GamesUpdated;
        }
    }
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
    /// <param name="progress">Reports the dll about to be worked on, for a progress display.</param>
    internal static Task<DllUpdateResult> UpdateGameAsync(Game game, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        // Copied because a swap refreshes the list underneath us.
        return UpdateGameAsync(game, new List<GameAssetType>(game.OutdatedAssetTypes), progress, cancellationToken);
    }

    /// <summary>
    /// Updates the named dlls in one game.
    /// </summary>
    /// <remarks>
    /// The list is a parameter so the preview sheet can hand back the subset the user left checked.
    /// The swap loop stays written once: "everything out of date" is just the list the other
    /// overload passes.
    /// </remarks>
    internal static async Task<DllUpdateResult> UpdateGameAsync(Game game, IReadOnlyList<GameAssetType> assetTypes, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new DllUpdateResult();

        foreach (var assetType in assetTypes)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var assetTypeName = DLLManager.Instance.GetAssetTypeName(assetType);
            progress?.Report($"{game.Title} - {assetTypeName}");

            var latestRecord = DLLManager.Instance.GetLatestRecord(assetType);
            if (latestRecord is null)
            {
                continue;
            }

            if (latestRecord.LocalRecord?.IsDownloaded == false)
            {
                // Cancellable, so pressing cancel during a large download stops it rather than
                // waiting for it to finish.
                var didDownload = await latestRecord.DownloadAsync(cancellationToken).ConfigureAwait(false);
                if (didDownload.Success == false)
                {
                    // A cancelled download is the user's doing, not a failure worth reporting.
                    if (didDownload.Cancelled == false)
                    {
                        result.Failures.Add($"{game.Title} - {assetTypeName}: {didDownload.Message}");
                    }

                    continue;
                }
            }

            var didUpdate = await game.UpdateDllAsync(latestRecord).ConfigureAwait(false);
            if (didUpdate.Success == false)
            {
                result.Failures.Add($"{game.Title} - {assetTypeName}: {didUpdate.Message}");
                continue;
            }

            ++result.Swapped;
        }

        if (result.Swapped > 0)
        {
            result.GamesUpdated = 1;
        }

        return result;
    }

    /// <summary>
    /// Restores every dll in a game that still has a backup.
    /// </summary>
    /// <remarks>
    /// A dll without a backup is left alone rather than reported as a failure. The game either
    /// never had that one swapped, or the backup went when a game update replaced the dll.
    /// </remarks>
    internal static async Task<DllUpdateResult> RevertGameAsync(Game game, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new DllUpdateResult();

        foreach (var assetType in GetRevertableAssetTypes(game))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var assetTypeName = DLLManager.Instance.GetAssetTypeName(assetType);
            progress?.Report($"{game.Title} - {assetTypeName}");

            var didReset = await game.ResetDllAsync(assetType).ConfigureAwait(false);
            if (didReset.Success == false)
            {
                result.Failures.Add($"{game.Title} - {assetTypeName}: {didReset.Message}");
                continue;
            }

            // A reset can succeed having restored only some locations, and says so in its message.
            if (string.IsNullOrEmpty(didReset.Message) == false)
            {
                result.Failures.Add($"{game.Title} - {assetTypeName}: {didReset.Message}");
            }

            ++result.Swapped;
        }

        if (result.Swapped > 0)
        {
            result.GamesUpdated = 1;
        }

        return result;
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
    /// Updates every out of date dll across the given games.
    /// </summary>
    internal static Task<DllUpdateResult> UpdateGamesAsync(IReadOnlyList<Game> games, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        return RunAcrossGamesAsync(games, UpdateGameAsync, progress, cancellationToken);
    }

    /// <summary>
    /// Restores every dll with a backup across the given games.
    /// </summary>
    internal static Task<DllUpdateResult> RevertGamesAsync(IReadOnlyList<Game> games, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        return RunAcrossGamesAsync(games, RevertGameAsync, progress, cancellationToken);
    }

    /// <summary>
    /// Updates exactly the files the preview sheet was left holding.
    /// </summary>
    /// <remarks>
    /// Grouped back into games because a swap is a per game operation, and run in the order the
    /// sheet listed them so the progress text reads down the sheet the user just approved.
    /// </remarks>
    internal static async Task<DllUpdateResult> UpdateSelectedAsync(IReadOnlyList<PendingDllUpdate> updates, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new DllUpdateResult();

        foreach (var updatesForGame in updates.GroupBy(x => x.Game))
        {
            // Checked here as well, for the same reason the batch runner checks it: this is a way
            // into the swap that does not pass through the other one.
            if (updatesForGame.Key.SkipUpdates)
            {
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var assetTypes = updatesForGame.Select(x => x.AssetType).ToList();
            result.Add(await UpdateGameAsync(updatesForGame.Key, assetTypes, progress, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    static async Task<DllUpdateResult> RunAcrossGamesAsync(
        IReadOnlyList<Game> games,
        Func<Game, IProgress<string>?, CancellationToken, Task<DllUpdateResult>> operation,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var result = new DllUpdateResult();

        foreach (var game in games)
        {
            // Enforced here as well as in the callers that build the list. This is the one place
            // every batch passes through, so a caller that forgets cannot write to a game the user
            // marked as leave alone.
            if (game.SkipUpdates)
            {
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Sequential on purpose. Running these in parallel would mean several games writing
            // dlls at once with no way to tell the user which one failed and why.
            result.Add(await operation(game, progress, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }
}
