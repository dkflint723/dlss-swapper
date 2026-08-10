using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DLSS_Swapper.Pages;

/// <summary>
/// One accent preset, as a swatch on the settings page.
/// </summary>
/// <remarks>
/// Painted from the preset's colour for the theme currently in use, because each one carries a
/// different value per theme -- the light ones are darkened so white text on them clears 4.5:1.
/// A swatch showing the dark value while the app is in light mode would be offering a colour the
/// app is never going to paint.
/// </remarks>
public partial class AccentSwatchItem : ObservableObject
{
    public required int Index { get; init; }

    /// <summary>Named as well as coloured, so the choice is not carried by colour alone.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// What picking this swatch runs.
    /// </summary>
    /// <remarks>
    /// Carried on the item rather than reached through the page with an ElementName binding. These
    /// swatches are declared in the page but end up inside a SettingsRow's content presenter, and a
    /// binding that resolves a name against its namescope stops finding the page once it is moved
    /// there. It fails silently: the button renders, and clicking it does nothing at all.
    /// </remarks>
    public required ICommand SelectCommand { get; init; }

    [ObservableProperty]
    public partial SolidColorBrush Fill { get; set; } = new SolidColorBrush();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionVisibility))]
    public partial bool IsSelected { get; set; }

    /// <summary>The ring around the chosen swatch. A ring, not a tick, so it reads at 26px.</summary>
    public Visibility SelectionVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    internal static AccentSwatchItem For(int index, AccentOption option, bool isDark, int selectedIndex, ICommand selectCommand)
    {
        return new AccentSwatchItem()
        {
            Index = index,
            Name = ResourceHelper.GetString(option.NameResourceKey),
            Fill = new SolidColorBrush(option.ForTheme(isDark)),
            IsSelected = index == selectedIndex,
            SelectCommand = selectCommand,
        };
    }
}
