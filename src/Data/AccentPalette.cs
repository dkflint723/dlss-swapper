using System.Collections.Generic;
using Windows.UI;

namespace DLSS_Swapper.Data;

/// <summary>
/// One selectable accent, with the colour to paint on it in each theme.
/// </summary>
/// <remarks>
/// Ink is carried with the accent rather than derived, because the light values are deliberately
/// darkened so white text clears 4.5:1 while the dark values are bright enough to need near black
/// text. Picking ink by luminance at runtime would get the brand green wrong in one theme or the
/// other.
/// </remarks>
public class AccentOption
{
    public required string Id { get; init; }

    /// <summary>Resource key for the display name. Not the name itself, so it can be translated.</summary>
    public required string NameResourceKey { get; init; }

    public required Color Dark { get; init; }

    public required Color DarkInk { get; init; }

    public required Color Light { get; init; }

    public required Color LightInk { get; init; }

    public Color ForTheme(bool isDark) => isDark ? Dark : Light;

    public Color InkForTheme(bool isDark) => isDark ? DarkInk : LightInk;
}

/// <summary>
/// The four accent presets from the redesign.
/// </summary>
public static class AccentPalette
{
    static Color FromHex(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);

    /// <summary>Brand green first, because it is the default and is sampled from the app icon.</summary>
    public static readonly IReadOnlyList<AccentOption> All =
    [
        new AccentOption()
        {
            Id = "brandGreen",
            NameResourceKey = "Settings_Accent_BrandGreen",
            Dark = FromHex(0x2E, 0xE0, 0x7A),
            DarkInk = FromHex(0x06, 0x21, 0x0F),

            // Darker than the handoff's #0E8A4F, twice over. White on that measured 4.41:1,
            // under the 4.5:1 the document requires of its own accents; and the accent is used as
            // text as well as a fill -- the newer version in the preview sheet, the link in the
            // sidebar -- where the light values were never checked at all. As text on the light
            // ground #0E8A4F is 3.5:1. This reaches 4.52:1 as text and 5.76:1 under white.
            Light = FromHex(0x0C, 0x75, 0x45),
            LightInk = FromHex(0xFF, 0xFF, 0xFF),
        },
        new AccentOption()
        {
            Id = "windowsBlue",
            NameResourceKey = "Settings_Accent_WindowsBlue",
            Dark = FromHex(0x4C, 0xC2, 0xFF),
            DarkInk = FromHex(0x06, 0x20, 0x2B),
            // 4.46:1 as text at #0067C0, just short. This clears it.
            Light = FromHex(0x00, 0x66, 0xBE),
            LightInk = FromHex(0xFF, 0xFF, 0xFF),
        },
        new AccentOption()
        {
            Id = "violet",
            NameResourceKey = "Settings_Accent_Violet",
            Dark = FromHex(0xB1, 0x8C, 0xFF),
            DarkInk = FromHex(0x1A, 0x0F, 0x33),
            Light = FromHex(0x6B, 0x3F, 0xD4),
            LightInk = FromHex(0xFF, 0xFF, 0xFF),
        },
        new AccentOption()
        {
            Id = "amber",
            NameResourceKey = "Settings_Accent_Amber",
            Dark = FromHex(0xFF, 0xC8, 0x3D),
            DarkInk = FromHex(0x2A, 0x1F, 0x00),
            // 3.94:1 as text at #B45309.
            Light = FromHex(0xA6, 0x4C, 0x08),
            LightInk = FromHex(0xFF, 0xFF, 0xFF),
        },
    ];

    public const int DefaultIndex = 0;

    /// <summary>
    /// The preset at an index, falling back to the default.
    /// </summary>
    /// <remarks>
    /// The index is persisted, so a settings file written by a build with more presets, or an
    /// edited one, must not take the app down over a colour.
    /// </remarks>
    public static AccentOption FromIndex(int index)
    {
        if (index < 0 || index >= All.Count)
        {
            return All[DefaultIndex];
        }

        return All[index];
    }
}
