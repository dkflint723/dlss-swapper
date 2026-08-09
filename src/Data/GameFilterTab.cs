using System.Collections.Generic;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.Data;

/// <summary>
/// One filter tab: its label, how many games it would show, and whether it is the active one.
/// </summary>
public class GameFilterTab
{
    public required GameFilter Filter { get; init; }

    public required string Label { get; init; }

    /// <summary>Reads as "7". Empty when this tab does not carry a count.</summary>
    public required string CountText { get; init; }

    public required Visibility CountVisibility { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>The 2px underline under the active tab.</summary>
    public Visibility UnderlineVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;

    public double LabelOpacity => IsActive ? 1.0 : 0.55;

    /// <summary>
    /// Builds a tab, counting with the same rule that decides what it shows.
    /// </summary>
    /// <param name="showCount">
    /// False for "All games", whose count is the library size and is already in the sidebar.
    /// </param>
    public static GameFilterTab For(
        GameFilter filter,
        string labelResourceKey,
        IReadOnlyList<Game> games,
        GameFilter activeFilter,
        bool showCount = true)
    {
        var count = showCount ? GameFilters.Count(games, filter) : 0;

        return new GameFilterTab()
        {
            Filter = filter,
            Label = ResourceHelper.GetString(labelResourceKey),
            CountText = count.ToString(System.Globalization.CultureInfo.CurrentCulture),

            // A tab with nothing in it shows no number rather than a zero, which reads as an error
            // rather than as "none".
            CountVisibility = showCount && count > 0 ? Visibility.Visible : Visibility.Collapsed,
            IsActive = filter == activeFilter,
        };
    }
}
