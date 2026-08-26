using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>
/// What one upscaler row on a game's page says: which upscaler, what is installed, and what is
/// available.
/// </summary>
/// <remarks>
/// The same shape as <see cref="GameRowStatus"/> and the same rules: a glyph plus a sentence, never
/// a colour, and no glyph at all for the quiet state, so a mark means something rather than being
/// the default. What it adds is the version — this is the one place in the app where the question
/// is "which build of this upscaler is in this game", rather than "is this game behind".
/// </remarks>
public class UpscalerRowStatus
{
    /// <summary>Private so only <see cref="For"/> can build one.</summary>
    private UpscalerRowStatus()
    {
    }

    public required GameAssetType AssetType { get; init; }

    /// <summary>The upscaler's name, already localised.</summary>
    public required string Title { get; init; }

    /// <summary>Reads as "v310.7 installed — v310.7 is the newest".</summary>
    public required string Sentence { get; init; }

    /// <summary>Segoe Fluent glyph, or empty when there is nothing worth marking.</summary>
    public required string Glyph { get; init; }

    /// <summary>What the row's control offers, or null when it offers nothing.</summary>
    public required string? ActionLabel { get; init; }

    /// <summary>True when this game is set to be left alone, which disables the row's control.</summary>
    public required bool IsLocked { get; init; }

    public static UpscalerRowStatus For(Game game, GameAssetType assetType)
    {
        // Read straight off the assets, the way GameEngines.Split does, rather than through the
        // asset slots: the slots are built by the app as it loads and are empty until it has, so a
        // row asking them would say "nothing installed" about a game that plainly has the dll.
        var installedAssets = game.GameAssets.Where(x => x.AssetType == assetType).ToList();
        var installed = installedAssets.FirstOrDefault()?.DisplayName ?? string.Empty;
        var newest = DLLManager.Instance.GetLatestRecord(assetType)?.DisplayVersion ?? string.Empty;

        var isBehind = game.OutdatedAssetTypes.Contains(assetType);
        // Game.HasSavedOriginal, per location, the same rule the games list, the row status and the
        // sidebar all read. This had its own copy asking whether a backup of the same TYPE existed
        // anywhere in the game - so a game with one dll in two folders and a copy beside only one of
        // them was reported as missing a copy by the list, and as having one by the very page you
        // open to do something about it. Every location has to be covered for the row to say so.
        var hasBackup = installedAssets.Count > 0 && installedAssets.All(game.HasSavedOriginal);

        // The same rule the asset slot applies, from the same list it applies it to.
        var multipleFound = installedAssets.Count > 1;

        return new UpscalerRowStatus()
        {
            AssetType = assetType,
            Title = DLLManager.Instance.GetAssetTypeName(assetType),
            Sentence = Describe(installed, newest, isBehind, hasBackup, multipleFound, game.SkipUpdates),
            Glyph = GlyphFor(isBehind, hasBackup, game.SkipUpdates),
            ActionLabel = string.IsNullOrEmpty(installed)
                ? ResourceHelper.GetString("GamePage_Row_Choose")
                : installed,
            IsLocked = game.SkipUpdates,
        };
    }

    /// <summary>
    /// The whole sentence, in the order the facts matter.
    /// </summary>
    /// <remarks>
    /// Behind first, because it is the only one that asks for a decision. "Left alone" beats it,
    /// because a game set to be skipped is not going to be updated whatever else is true, and
    /// saying it is behind would invite a click that the row then refuses.
    /// </remarks>
    static string Describe(string installed, string newest, bool isBehind, bool hasSavedOriginal, bool multipleFound, bool skipUpdates)
    {
        var parts = new List<string>();

        if (string.IsNullOrEmpty(installed))
        {
            parts.Add(ResourceHelper.GetString("GamePage_Row_NothingInstalled"));
        }
        else if (skipUpdates)
        {
            parts.Add(ResourceHelper.GetFormattedResourceTemplate("GamePage_Row_InstalledLeftAloneTemplate", installed));
        }
        else if (isBehind && string.IsNullOrEmpty(newest) == false)
        {
            parts.Add(ResourceHelper.GetFormattedResourceTemplate("GamePage_Row_BehindTemplate", installed, newest));
        }
        else
        {
            parts.Add(ResourceHelper.GetFormattedResourceTemplate("GamePage_Row_InstalledNewestTemplate", installed));
        }

        // Said on the row rather than behind an icon button, which is where it used to live: the
        // fact that a game has two copies of a dll changes what a swap will do to it.
        if (multipleFound)
        {
            parts.Add(ResourceHelper.GetString("GamePage_Row_MultipleCopies"));
        }

        // Only worth saying when there is something installed to have kept an original of.
        if (string.IsNullOrEmpty(installed) == false && hasSavedOriginal == false)
        {
            parts.Add(ResourceHelper.GetString("GamePage_Row_NoSavedOriginal"));
        }

        return string.Join(ResourceHelper.GetString("GamePage_Row_ClauseSeparator"), parts);
    }

    /// <summary>
    /// The mark beside the sentence, or none.
    /// </summary>
    /// <remarks>
    /// A row that is current and has its original saved gets no glyph. That is most rows on most
    /// games, and marking all of them would make the absence of a mark the exceptional case.
    /// </remarks>
    static string GlyphFor(bool isBehind, bool hasSavedOriginal, bool skipUpdates)
    {
        if (skipUpdates)
        {
            return "";
        }

        if (isBehind)
        {
            return "";
        }

        if (hasSavedOriginal == false)
        {
            return "";
        }

        return string.Empty;
    }
}

/// <summary>
/// The rows a game's page shows, and the one line naming what it does not have.
/// </summary>
/// <remarks>
/// Both from one split, so the rows on screen and the number in the sentence cannot disagree —
/// which they could before, because the per-upscaler control worked out for itself whether the game
/// had that dll instead of asking <see cref="GameEngines"/>.
/// </remarks>
public class UpscalerRows
{
    public required IReadOnlyList<UpscalerRowStatus> Rows { get; init; }

    /// <summary>Reads as "3 upscalers not in this game — FSR 3.1 Vulkan, XeSS FG, XeLL".</summary>
    public required string AbsentSummary { get; init; }

    public static UpscalerRows For(Game game)
    {
        var split = GameEngines.Split(game);
        var rows = new List<UpscalerRowStatus>(split.Present.Count);

        foreach (var assetType in split.Present)
        {
            rows.Add(UpscalerRowStatus.For(game, assetType));
        }

        return new UpscalerRows()
        {
            Rows = rows,
            AbsentSummary = split.AbsentSummary,
        };
    }
}
