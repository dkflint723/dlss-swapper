using System;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.SteamGridDb;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// A cover the scan is certain about, waiting to be applied.
/// </summary>
/// <remarks>
/// Every one of these is ticked when the list is built, because every one of these is a name match
/// beyond doubt - that is the only way an entry gets here. Unticking is for taste rather than for
/// correcting the match.
/// </remarks>
public partial class CoverScanReadyItem : ObservableObject
{
    public CoverScanEntry Entry { get; }

    public string Title { get; }

    /// <summary>What SteamGridDB called it, so the agreement is visible rather than assumed.</summary>
    public string MatchedName { get; }

    public Uri ThumbnailUri { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    public CoverScanReadyItem(CoverScanEntry entry)
    {
        Entry = entry;
        Title = entry.Game.Title;
        MatchedName = ResourceHelper.GetFormattedResourceTemplate("CoverScan_MatchedTemplate", entry.MatchedName ?? string.Empty);
        ThumbnailUri = new Uri(entry.Image?.ThumbnailUrl ?? "about:blank");
    }
}

/// <summary>
/// A game the scan will not touch, and the reason why.
/// </summary>
/// <remarks>
/// Named rather than counted. "Six games need attention" is not something anybody can act on; six
/// titles with a reason each is a list you can work through with the per-game picker.
/// </remarks>
public partial class CoverScanNeedsYouItem : ObservableObject
{
    /// <summary>The game itself, so picking one of these can open a picker for it.</summary>
    public Game Game { get; }

    public string Title { get; }

    /// <summary>
    /// Why the scan would not do it, and afterwards that it has been done.
    /// </summary>
    /// <remarks>
    /// The row says so in words rather than going grey or gaining a tick. Half this list is read by
    /// someone working down it, and "done" has to survive being read rather than looked at.
    /// </remarks>
    [ObservableProperty]
    public partial string Reason { get; set; }

    public CoverScanNeedsYouItem(CoverScanEntry entry)
    {
        Game = entry.Game;
        Title = entry.Game.Title;
        Reason = ReasonFor(entry);
    }

    /// <summary>Called once a cover has been picked for this game by hand.</summary>
    public void MarkResolved()
    {
        Reason = ResourceHelper.GetString("CoverScan_CoverSet");
    }

    static string ReasonFor(CoverScanEntry entry)
    {
        return entry.Outcome switch
        {
            CoverScanOutcome.NotConfident => ResourceHelper.GetFormattedResourceTemplate(
                "CoverScan_Reason_NotConfident", entry.MatchedName ?? string.Empty),
            CoverScanOutcome.NoMatches => ResourceHelper.GetString("CoverScan_Reason_NoMatches"),
            CoverScanOutcome.NoArt => ResourceHelper.GetString("CoverScan_Reason_NoArt"),
            _ => ResourceHelper.GetString("CoverScan_Reason_Failed"),
        };
    }
}
