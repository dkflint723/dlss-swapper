using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.Pages;

/// <summary>
/// Page for application settings.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public static string PageTag { get; } = "PageTag_Settings";

    public SettingsPageModel ViewModel { get; private set; }

    public SettingsPage()
    {
        this.InitializeComponent();
        ViewModel = new SettingsPageModel(this);
        DataContext = ViewModel;

        // The accent swatches are painted per theme, and this is the event that knows the theme
        // actually changed. Refreshing them from the button that requested it fires before the
        // change has taken, and "use system setting" does not say which theme it resolves to.
        ActualThemeChanged += (sender, args) => ViewModel.RefreshAccentSwatches();
    }
}
