using System;
using System.Collections.Generic;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>
/// What the games page says when swaps have been undone behind the app's back.
/// </summary>
/// <remarks>
/// <para>
/// The scan has always recorded these moments (see <see cref="UndoneSwapFinder"/>), and the record
/// was the only place they went: to learn a patch had thrown away your swap you had to open that
/// game and read its history dialog, which nobody does unprompted. This is the sentence version,
/// shown where the games are.
/// </para>
/// <para>
/// Dismissing stores a high-water mark rather than a flag, so closing the bar acknowledges what it
/// showed and only that: a swap undone later reopens it. Type names come off the definitions the
/// way <see cref="DllUpdateRunner.GetRevertPreview"/> reads them, for the same reason — a test can
/// build a notice without standing up the manager.
/// </para>
/// </remarks>
public sealed class UndoneSwapNotice
{
    public required string Title { get; init; }

    public required string Message { get; init; }

    /// <summary>The newest change this notice covers. Dismissing it stores this.</summary>
    public required DateTime NewestChangedAt { get; init; }

    /// <summary>
    /// The notice for what is new since the last dismissal, or null when there is nothing to say.
    /// </summary>
    /// <param name="gameTitlesById">
    /// Who to name. An undone swap in a game that is no longer in the library is left out: there
    /// is no row on the page to act on.
    /// </param>
    public static UndoneSwapNotice? For(
        IReadOnlyList<UndoneSwap> undoneSwaps,
        IReadOnlyDictionary<string, string> gameTitlesById,
        DateTime dismissedAt)
    {
        var lines = new List<string>();
        var newestChangedAt = DateTime.MinValue;

        foreach (var undoneSwap in undoneSwaps)
        {
            if (undoneSwap.ChangedAt <= dismissedAt)
            {
                continue;
            }

            if (gameTitlesById.TryGetValue(undoneSwap.GameId, out var gameTitle) == false)
            {
                continue;
            }

            if (DllTypes.ForAssetType(undoneSwap.AssetType) is not DllTypeDefinition definition)
            {
                continue;
            }

            // The "Game — Type" shape every batch surface here uses.
            lines.Add($"{gameTitle} — {ResourceHelper.GetString(definition.DisplayNameResourceKey)}");

            if (undoneSwap.ChangedAt > newestChangedAt)
            {
                newestChangedAt = undoneSwap.ChangedAt;
            }
        }

        if (lines.Count == 0)
        {
            return null;
        }

        return new UndoneSwapNotice()
        {
            Title = lines.Count == 1
                ? ResourceHelper.GetString("UndoneSwaps_TitleOne")
                : ResourceHelper.GetFormattedResourceTemplate("UndoneSwaps_TitleTemplate", lines.Count),
            Message = ResourceHelper.GetString("UndoneSwaps_Body")
                + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, lines),
            NewestChangedAt = newestChangedAt,
        };
    }
}
