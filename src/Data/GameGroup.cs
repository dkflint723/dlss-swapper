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
    /// Whether a search is narrowing the list, which suspends folding.
    /// </summary>
    /// <remarks>
    /// A search looks through folded sections. Without this, searching for a game that lives in one
    /// looked exactly like searching for a game that is not installed.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFolded))]
    [NotifyPropertyChangedFor(nameof(ShowsFoldControl))]
    [NotifyPropertyChangedFor(nameof(ShowsHeader))]
    public partial bool IsSearchActive { get; set; }

    /// <summary>Only launcher sections fold. Favourites cuts across them, and the ungrouped list has no header at all.</summary>
    public bool IsCollapsible => GameLibrary is not null;

    /// <summary>Whether this section is actually holding its games back right now.</summary>
    public bool IsFolded => IsCollapsible && IsExpanded == false && IsSearchActive == false;

    /// <summary>
    /// Whether the heading offers to fold.
    /// </summary>
    /// <remarks>
    /// It does not during a search, because folding is suspended then and the click would do
    /// nothing visible. Taking the chevron away with it also stops it pointing the wrong way at a
    /// section the search has just opened.
    /// </remarks>
    public bool ShowsFoldControl => IsCollapsible && IsSearchActive == false;

    public string ChevronGlyph => IsExpanded ? "\uE70D" : "\uE76C";

    /// <summary>
    /// Whether this section's heading is drawn.
    /// </summary>
    /// <remarks>
    /// This is what HidesIfEmpty used to do, and it has to be done here instead, because a folded
    /// section is empty by that same test and its heading is the only way back. An empty section
    /// still disappears - a launcher you own no games on, or every launcher but one while a search
    /// is running - but a folded one keeps its heading, because the user is the one who folded it.
    /// During a search nothing is folded, so every section is judged on what it matched.
    /// </remarks>
    public bool ShowsHeader => string.IsNullOrEmpty(Name) == false && (IsFolded || Games.Count > 0);

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
