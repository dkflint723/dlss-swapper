using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace DLSS_Swapper;

/// <summary>
/// Keeps the accent brushes in the application resources in step with the settings and the theme.
/// </summary>
/// <remarks>
/// <para>
/// The accent cannot live in a theme dictionary because it is chosen at runtime, so the brushes are
/// declared once in App.xaml and have their colour replaced here. Mutating the colour on a brush
/// that is already referenced repaints everything bound to it, which is what lets the theme and
/// accent change without a restart.
/// </para>
/// <para>
/// Nothing here may run before the window exists. Constructing UISettings, and reading
/// Application.Current.RequestedTheme, both fail during the App constructor in an unpackaged app,
/// and a throw there takes the process down before any window or log line appears. Start is
/// therefore called from OnLaunched, and every earlier Apply is a no-op rather than an error,
/// because settings loaded at startup will call it long before then.
/// </para>
/// </remarks>
internal static class AccentManager
{
    internal const string AccentBrushKey = "DsAccentBrush";
    internal const string AccentInkBrushKey = "DsAccentInkBrush";

    static UISettings? _uiSettings;
    static bool _started;

    /// <summary>
    /// Begins painting the accent, and follows the Windows personalisation colour.
    /// </summary>
    /// <remarks>Call once the main window exists.</remarks>
    internal static void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        try
        {
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += (sender, args) =>
            {
                if (Settings.Instance.MatchDesktopAccent)
                {
                    // Fires off the UI thread.
                    UiThread.Run(Apply);
                }
            };
        }
        catch (System.Exception err)
        {
            // Without UISettings the desktop accent is simply unavailable; the presets still work.
            Logger.Error(err);
        }

        Apply();
    }

    /// <summary>Recomputes the accent and repaints anything using it.</summary>
    internal static void Apply()
    {
        if (_started == false)
        {
            return;
        }

        try
        {
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
        catch (System.Exception err)
        {
            // A wrong accent is a cosmetic problem. Taking the app down over one is not.
            Logger.Error(err);
        }
    }

    /// <summary>
    /// Which theme the accent is currently being painted for.
    /// </summary>
    /// <remarks>
    /// Exposed for the settings swatches, which have to show the same value the app would actually
    /// use. Each preset carries a different colour per theme, so a swatch painted from the wrong
    /// one would offer a colour the app never paints.
    /// </remarks>
    internal static bool IsDark => IsDarkTheme();

    static bool IsDarkTheme()
    {
        var appTheme = (ElementTheme)Settings.Instance.AppTheme;
        if (appTheme == ElementTheme.Light)
        {
            return false;
        }

        if (appTheme == ElementTheme.Dark)
        {
            return true;
        }

        // Following the system. Read it from the window rather than from Application, whose
        // RequestedTheme is not safe to touch outside a narrow window during startup.
        var root = App.CurrentApp?.MainWindow?.Content as FrameworkElement;
        return root?.ActualTheme != ElementTheme.Light;
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
