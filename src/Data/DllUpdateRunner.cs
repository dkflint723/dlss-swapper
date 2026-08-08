using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    internal static async Task<DllUpdateResult> UpdateGameAsync(Game game, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new DllUpdateResult();

        // Copied because a swap refreshes the list underneath us.
        var outdatedAssetTypes = new List<GameAssetType>(game.OutdatedAssetTypes);

        foreach (var assetType in outdatedAssetTypes)
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
                var didDownload = await latestRecord.DownloadAsync().ConfigureAwait(false);
                if (didDownload.Success == false)
                {
                    result.Failures.Add($"{game.Title} - {assetTypeName}: {didDownload.Message}");
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
    /// Updates every out of date dll across the given games.
    /// </summary>
    internal static async Task<DllUpdateResult> UpdateGamesAsync(IReadOnlyList<Game> games, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new DllUpdateResult();

        foreach (var game in games)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Sequential on purpose. Running these in parallel would mean several games writing
            // dlls at once with no way to tell the user which one failed and why.
            result.Add(await UpdateGameAsync(game, progress, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }
}
