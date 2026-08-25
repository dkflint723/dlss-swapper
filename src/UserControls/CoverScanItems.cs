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

    /// <summary>
    /// What SteamGridDB called it, so the agreement is visible rather than assumed - and afterwards,
    /// that the cover has been set.
    /// </summary>
    /// <remarks>
    /// Rewritten rather than only unticked. A row that has been written is no longer a proposal, and
    /// an empty tick box is a shape rather than a sentence: it reads the same as one somebody
    /// unticked on purpose. The uncertain list below says "Cover set." the same way.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleDescription))]
    public partial string MatchedName { get; set; }

    public Uri ThumbnailUri { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    /// <summary>
    /// The row in one line, for the tick box that decides whether it is written.
    /// </summary>
    /// <remarks>
    /// The title and the matched name are sibling TextBlocks in another column, so the tick box on
    /// its own announced as "checkbox, checked" with no game named - a column of them directly
    /// above a button that writes covers into a library.
    /// </remarks>
    public string AccessibleDescription =>
        ResourceHelper.GetFormattedResourceTemplate("CoverScan_RowDescriptionTemplate", Title, MatchedName);

    /// <summary>
    /// Says this cover has been written, and takes the row out of the proposal set.
    /// </summary>
    /// <remarks>
    /// Unticking is what stops Apply offering to do it again. See the invariant on
    /// <c>CoverScanModel._applied</c> for why a second write is destructive rather than wasteful.
    /// </remarks>
    public void MarkApplied()
    {
        IsSelected = false;
        MatchedName = ResourceHelper.GetString("CoverScan_CoverSet");
    }

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
