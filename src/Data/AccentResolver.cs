using Windows.UI;

namespace DLSS_Swapper.Data;

/// <summary>An accent colour and the colour to paint on top of it.</summary>
public readonly record struct ResolvedAccent(Color Accent, Color Ink);

/// <summary>
/// Works out which accent is in force, and what colour is readable on it.
/// </summary>
/// <remarks>
/// Separate from applying it so the decision can be tested. Choosing the wrong ink produces text
/// that renders but cannot be read, which a passing build will not tell you about.
/// </remarks>
public static class AccentResolver
{
    /// <summary>Relative luminance per WCAG 2.1.</summary>
    static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var sRgb = value / 255.0;
            return sRgb <= 0.04045 ? sRgb / 12.92 : System.Math.Pow((sRgb + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    static double ContrastRatio(Color a, Color b)
    {
        var lighter = System.Math.Max(RelativeLuminance(a), RelativeLuminance(b));
        var darker = System.Math.Min(RelativeLuminance(a), RelativeLuminance(b));

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>Near black rather than pure black, matching the ink tones the presets use.</summary>
    static readonly Color _darkInk = Color.FromArgb(255, 0x0C, 0x0D, 0x0F);
    static readonly Color _lightInk = Color.FromArgb(255, 0xFF, 0xFF, 0xFF);

    /// <summary>
    /// Picks the more readable of white and near black for an arbitrary accent.
    /// </summary>
    /// <remarks>
    /// Only used for the desktop accent, which the user can set to anything and which arrives with
    /// no ink colour of its own. The presets carry hand picked ink instead, because a preset's ink
    /// is tuned rather than merely adequate.
    /// </remarks>
    public static Color InkFor(Color accent)
    {
        return ContrastRatio(accent, _lightInk) >= ContrastRatio(accent, _darkInk)
            ? _lightInk
            : _darkInk;
    }

    /// <summary>
    /// Resolves the accent in force.
    /// </summary>
    /// <param name="presetIndex">Index into AccentPalette.All.</param>
    /// <param name="isDarkTheme">Which theme's value to take.</param>
    /// <param name="desktopAccent">
    /// The Windows personalisation colour, or null when not matching the desktop. When supplied it
    /// wins over the preset.
    /// </param>
    public static ResolvedAccent Resolve(int presetIndex, bool isDarkTheme, Color? desktopAccent)
    {
        if (desktopAccent is not null)
        {
            return new ResolvedAccent(desktopAccent.Value, InkFor(desktopAccent.Value));
        }

        var preset = AccentPalette.FromIndex(presetIndex);
        return new ResolvedAccent(preset.ForTheme(isDarkTheme), preset.InkForTheme(isDarkTheme));
    }
}
