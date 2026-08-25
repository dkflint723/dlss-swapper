using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.CoverArt;

namespace DLSS_Swapper.Data.SteamGridDb;

/// <summary>Why a game did or did not come out of a scan with a cover to apply.</summary>
public enum CoverScanOutcome
{
    /// <summary>A certain name match with portrait art behind it. The only outcome that writes.</summary>
    Ready,

    /// <summary>Results came back, but none of them are certainly this game.</summary>
    NotConfident,

    /// <summary>The search matched nothing at all.</summary>
    NoMatches,

    /// <summary>The right game, but it has no portrait art - only the wide shapes we cannot use.</summary>
    NoArt,

    /// <summary>The scan failed for this game, which says nothing about whether art exists.</summary>
    Failed,
}

/// <summary>What a scan found for one game.</summary>
public sealed class CoverScanEntry
{
    public required Game Game { get; init; }

    public required CoverScanOutcome Outcome { get; init; }

    /// <summary>The art to apply. Only ever set when the outcome is <see cref="CoverScanOutcome.Ready"/>.</summary>
    public CoverArtImage? Image { get; init; }

    /// <summary>
    /// The name SteamGridDB used, so a person can see what it was about to agree to.
    /// </summary>
    /// <remarks>
    /// For a match this is the matched name. For a near miss it is the closest thing the search
    /// returned, which is what tells you whether it is worth opening that game by hand.
    /// </remarks>
    public string? MatchedName { get; init; }
}

public sealed class CoverScanProgress
{
    public required int Done { get; init; }

    public required int Total { get; init; }

    public required string CurrentTitle { get; init; }
}

/// <summary>
/// Looks for a cover for every game, and is honest about which answers it is sure of.
/// </summary>
/// <remarks>
/// <para>
/// A scan cannot ask about each game the way the picker does, so it does the opposite: it only ever
/// proposes a cover where the name matches beyond doubt, and hands back everything else by name so
/// a person can open those games and choose. See <see cref="CoverArtMatch"/> for what counts.
/// </para>
/// <para>
/// A game whose name is not certain costs one request rather than two, because there is no point
/// fetching art for a game we are not going to offer.
/// </para>
/// </remarks>
internal static class CoverScanRunner
{
    /// <summary>
    /// Between requests, so a library of two dozen games does not arrive at SteamGridDB as a burst.
    /// Costs a few seconds across a whole scan and is the difference between a guest and a problem.
    /// </summary>
    static readonly TimeSpan _betweenRequests = TimeSpan.FromMilliseconds(150);

    internal static async Task<IReadOnlyList<CoverScanEntry>> ScanAsync(
        IReadOnlyList<Game> games,
        IProgress<CoverScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<CoverScanEntry>();

        for (var index = 0; index < games.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var game = games[index];

            progress?.Report(new CoverScanProgress()
            {
                Done = index,
                Total = games.Count,
                CurrentTitle = game.Title,
            });

            entries.Add(await ScanOneAsync(game, cancellationToken).ConfigureAwait(false));

            if (index < games.Count - 1)
            {
                await Task.Delay(_betweenRequests, cancellationToken).ConfigureAwait(false);
            }
        }

        progress?.Report(new CoverScanProgress()
        {
            Done = games.Count,
            Total = games.Count,
            CurrentTitle = string.Empty,
        });

        return entries;
    }

    static async Task<CoverScanEntry> ScanOneAsync(Game game, CancellationToken cancellationToken)
    {
        try
        {
            var results = await SteamGridDbClient.SearchAsync(game.Title, cancellationToken).ConfigureAwait(false);

            if (results.Count == 0)
            {
                return new CoverScanEntry() { Game = game, Outcome = CoverScanOutcome.NoMatches };
            }

            var match = CoverArtMatch.FirstConfident(game.Title, results);

            if (match is null)
            {
                // Named anyway. "Nothing certain" is not the same as "nothing there", and the
                // closest name is what tells someone whether opening that game is worth it.
                return new CoverScanEntry()
                {
                    Game = game,
                    Outcome = CoverScanOutcome.NotConfident,
                    MatchedName = results[0].Name,
                };
            }

            await Task.Delay(_betweenRequests, cancellationToken).ConfigureAwait(false);

            var images = await SteamGridDbClient.GetPortraitsAsync(match.Id, cancellationToken).ConfigureAwait(false);

            if (images.Count == 0)
            {
                return new CoverScanEntry()
                {
                    Game = game,
                    Outcome = CoverScanOutcome.NoArt,
                    MatchedName = match.Name,
                };
            }

            // The first, which is SteamGridDB's own ranking.
            //
            // Measured rather than read: its score, upvote and downvote fields come back zero on
            // every result, and an "order" parameter is accepted with a nonsense value and changes
            // nothing. The published api docs sit behind Cloudflare and could not be fetched, so
            // other parameter spellings were never ruled out - if a real sort turns up, it belongs
            // in CoverArtQuery.PortraitQuery, which is the one tested place that builds this query.
            // Either way there is no download count to show anyone, and none is claimed.
            return new CoverScanEntry()
            {
                Game = game,
                Outcome = CoverScanOutcome.Ready,
                MatchedName = match.Name,
                Image = images[0],
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Cover scan failed for {game.Title}.");

            return new CoverScanEntry() { Game = game, Outcome = CoverScanOutcome.Failed };
        }
    }

    /// <summary>
    /// Writes one scanned cover, keeping whatever it replaced so the batch can be put back.
    /// </summary>
    /// <returns>
    /// Whether a cover was written, and the path of the backup taken - null when there was no
    /// custom cover to keep, or when nothing was written.
    /// </returns>
    /// <remarks>
    /// The download happens before the backup is taken, deliberately. Copying first meant a
    /// download that failed or was cancelled left a <c>.before_scan</c> file behind that nothing
    /// could ever read or delete: the path died with the stack frame, so the batch never learned
    /// of it and closing the dialog cleaned up only the ones it knew about.
    /// </remarks>
    internal static async Task<(bool Written, string? BackupPath)> ApplyAsync(CoverScanEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.Image is null)
        {
            return (false, null);
        }

        using var stream = await SteamGridDbClient.DownloadAsync(entry.Image.Url, cancellationToken).ConfigureAwait(false);

        var coverPath = entry.Game.ExpectedCustomCoverImage;
        string? backupPath = null;

        // A game that already had a custom cover is the only case where undo cannot simply delete
        // what we wrote, so that file is kept next to it until the strip is dismissed.
        if (File.Exists(coverPath))
        {
            backupPath = coverPath + ".before_scan";

            File.Copy(coverPath, backupPath, overwrite: true);
        }

        var written = await Task.Run(() => entry.Game.AddCustomCover(stream), cancellationToken).ConfigureAwait(false);

        if (written == false)
        {
            // AddCustomCover writes beside the target and moves, so a failure leaves the existing
            // cover untouched - which means this copy is a backup of something nothing replaced.
            // Removing it here is what keeps that from being the orphan described above.
            DeleteIfPresent(backupPath);

            return (false, null);
        }

        return (true, backupPath);
    }

    static void DeleteIfPresent(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }
}
