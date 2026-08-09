using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Collections;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Data;

internal partial class GameGroup : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public GameLibrary? GameLibrary { get; init; }
    public AdvancedCollectionView Games { get; init; }

    /// <summary>
    /// Whether this section's games are showing.
    /// </summary>
    /// <remarks>
    /// Folding filters the games out of the group rather than hiding their rows. Hiding rows works
    /// in the list and not in the grid: the grid's panel takes its cell size from the first item it
    /// measures, so folding the first section made every card in every section zero-sized.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronGlyph))]
    [NotifyPropertyChangedFor(nameof(IsFolded))]
    [NotifyPropertyChangedFor(nameof(ShowsHeader))]
    public partial bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Whether the page is showing a subset of the library, which suspends folding.
    /// </summary>
    /// <remarks>
    /// A search, or any tab but "All games", is a question about the whole library and has to be
    /// answered from all of it. Without this, asking for the games with an update and asking for a
    /// game by name both gave the same wrong answer about a folded launcher: nothing here.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFolded))]
    public partial bool IsListNarrowed { get; set; }

    /// <summary>Only launcher sections fold. Favourites cuts across them, and the ungrouped list has no header at all.</summary>
    public bool IsCollapsible => GameLibrary is not null;

    /// <summary>Whether this section is actually holding its games back right now.</summary>
    public bool IsFolded => IsCollapsible && IsExpanded == false && IsListNarrowed == false;

    /// <summary>
    /// The chevron, from the stored fold state rather than <see cref="IsFolded"/>.
    /// </summary>
    /// <remarks>
    /// So it keeps pointing at what a click will do. During a search a folded section shows its
    /// matches with the chevron still closed, which is the truth: the section is folded and the
    /// search is looking inside it.
    /// </remarks>
    public string ChevronGlyph => IsExpanded ? "\uE70D" : "\uE76C";

    /// <summary>
    /// Whether this section's heading is drawn.
    /// </summary>
    /// <remarks>
    /// This is what HidesIfEmpty used to do, and it has to be done here instead, because a folded
    /// section is empty by that same test and its heading is the only way back. An empty section
    /// still disappears - a launcher you own no games on, or every launcher but one while a search
    /// is running - but a folded one keeps its heading, because the user is the one who folded it.
    ///
    /// Deliberately the stored fold state rather than <see cref="IsFolded"/>, which would also hide
    /// the heading of a folded section that a search did not match. That reads better and does not
    /// work: a heading only comes back when its section's membership changes, and a folded section
    /// is empty on both sides of a search, so the heading went and stayed gone, leaving no way to
    /// unfold it. Measured, not assumed - rebuilding the whole view did not bring it back either.
    /// Written this way, a heading only ever has to appear when games appear with it.
    /// </remarks>
    public bool ShowsHeader => string.IsNullOrEmpty(Name) == false && (IsExpanded == false || Games.Count > 0);

    public GameGroup(string name, GameLibrary? gameLibrary, AdvancedCollectionView games)
    {
        Name = name;
        GameLibrary = gameLibrary;
        Games = games;

        // Observed rather than worked out once: games arrive long after this is built, and a search
        // empties and refills every one of these on each keystroke.
        Games.VectorChanged += (sender, args) => OnPropertyChanged(nameof(ShowsHeader));

        if (gameLibrary is not null)
        {
            var librarySettings = Settings.Instance.GameLibrarySettings.FirstOrDefault(x => x.GameLibrary == gameLibrary);
            IsExpanded = librarySettings?.IsCollapsed != true;
        }
    }

    /// <summary>Folds or unfolds the section, and remembers it.</summary>
    public void ToggleExpanded()
    {
        if (IsCollapsible == false)
        {
            return;
        }

        IsExpanded = !IsExpanded;

        // The filter asks the group whether it is folded, so it has to be run again to notice.
        Games.RefreshFilter();

        var librarySettings = Settings.Instance.GameLibrarySettings.FirstOrDefault(x => x.GameLibrary == GameLibrary);
        if (librarySettings is not null)
        {
            librarySettings.IsCollapsed = IsExpanded == false;
            Settings.Instance.SaveJson();
        }
    }
}
