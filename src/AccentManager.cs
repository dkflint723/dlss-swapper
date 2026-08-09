using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace DLSS_Swapper;

/// <summary>
/// Keeps the accent brushes in the application resources in step with the settings and the theme.
/// </summary>
/// <remarks>
/// The accent cannot live in a theme dictionary because it is chosen at runtime, so the brushes are
/// declared once in App.xaml and have their colour replaced here. Mutating the colour on a brush
/// that is already referenced repaints everything bound to it, which is what lets the theme and
/// accent change without a restart.
/// </remarks>
internal static class AccentManager
{
    internal const string AccentBrushKey = "DsAccentBrush";
    internal const string AccentInkBrushKey = "DsAccentInkBrush";

    static UISettings? _uiSettings;

    /// <summary>
    /// Starts following the Windows personalisation colour.
    /// </summary>
    /// <remarks>
    /// Subscribed to unconditionally rather than only while the setting is on, because the event is
    /// cheap and the alternative is subscribing and unsubscribing from a settings setter that can
    /// run before the window exists.
    /// </remarks>
    internal static void Start()
    {
        if (_uiSettings is not null)
        {
            return;
        }

        _uiSettings = new UISettings();
        _uiSettings.ColorValuesChanged += (sender, args) =>
        {
            if (Settings.Instance.MatchDesktopAccent)
            {
                // Fires off the UI thread.
                UiThread.Run(Apply);
            }
        };

        Apply();
    }

    /// <summary>Recomputes the accent and repaints anything using it.</summary>
    internal static void Apply()
    {
        var app = App.CurrentApp;
        if (app is null)
        {
            // No application, so no resources to update. This is the case under test.
            return;
        }

        Color? desktopAccent = null;
        if (Settings.Instance.MatchDesktopAccent && _uiSettings is not null)
        {
            desktopAccent = _uiSettings.GetColorValue(UIColorType.Accent);
        }

        var resolved = AccentResolver.Resolve(
            Settings.Instance.AccentPreset,
            IsDarkTheme(),
            desktopAccent);

        SetBrushColor(AccentBrushKey, resolved.Accent);
        SetBrushColor(AccentInkBrushKey, resolved.Ink);
    }

    static bool IsDarkTheme()
    {
        return Settings.Instance.AppTheme switch
        {
            ElementTheme.Light => false,
            ElementTheme.Dark => true,

            // Following the system, so take whatever the application resolved to.
            _ => Application.Current.RequestedTheme == ApplicationTheme.Dark,
        };
    }

    static void SetBrushColor(string key, Color color)
    {
        if (Application.Current.Resources.TryGetValue(key, out var resource)
            && resource is Microsoft.UI.Xaml.Media.SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }
}
