using System.Linq;
using DLSS_Swapper.Data;
using Windows.UI;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the accent presets, and specifically that every accent and ink pair is readable.
/// </summary>
/// <remarks>
/// The design pairs each accent with an ink colour rather than deriving one, because the light
/// values are darkened for white text while the dark values need near black text. A wrong pairing
/// produces text that is technically rendered and practically unreadable, which is the kind of
/// thing that survives a visual review.
/// </remarks>
public class AccentPaletteTests
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
        var luminanceA = RelativeLuminance(a);
        var luminanceB = RelativeLuminance(b);
        var lighter = System.Math.Max(luminanceA, luminanceB);
        var darker = System.Math.Min(luminanceA, luminanceB);

        return (lighter + 0.05) / (darker + 0.05);
    }

    [Fact]
    public void ThereAreFourPresets()
    {
        Assert.Equal(4, AccentPalette.All.Count);
    }

    [Fact]
    public void BrandGreenIsTheDefault()
    {
        Assert.Equal("brandGreen", AccentPalette.FromIndex(AccentPalette.DefaultIndex).Id);
    }

    [Fact]
    public void PresetIdsAreUnique()
    {
        Assert.Equal(AccentPalette.All.Count, AccentPalette.All.Select(x => x.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryAccentAndInkPairClearsWcagAa(bool isDark)
    {
        // 4.5:1 is the AA threshold for normal sized text, which is what sits on accent buttons.
        foreach (var accent in AccentPalette.All)
        {
            var ratio = ContrastRatio(accent.ForTheme(isDark), accent.InkForTheme(isDark));

            Assert.True(
                ratio >= 4.5,
                $"{accent.Id} in {(isDark ? "dark" : "light")} theme is only {ratio:F2}:1");
        }
    }

    [Fact]
    public void DarkAndLightValuesDifferForEveryPreset()
    {
        // The light values are deliberately darkened. If a preset ever ends up with the same colour
        // in both themes it has lost that adjustment.
        foreach (var accent in AccentPalette.All)
        {
            Assert.NotEqual(accent.Dark, accent.Light);
        }
    }

    [Fact]
    public void AnOutOfRangeIndexFallsBackToTheDefault()
    {
        // The index is persisted, so a settings file from another build must not crash the app.
        Assert.Equal("brandGreen", AccentPalette.FromIndex(-1).Id);
        Assert.Equal("brandGreen", AccentPalette.FromIndex(99).Id);
    }

    [Fact]
    public void EveryPresetIsReachableByItsIndex()
    {
        for (var index = 0; index < AccentPalette.All.Count; index += 1)
        {
            Assert.Equal(AccentPalette.All[index].Id, AccentPalette.FromIndex(index).Id);
        }
    }

    [Fact]
    public void BrandGreenMatchesTheDesignValues()
    {
        // Pinned because these are sampled from the app icon and quoted exactly in the handoff.
        var brandGreen = AccentPalette.FromIndex(0);

        Assert.Equal(Color.FromArgb(255, 0x2E, 0xE0, 0x7A), brandGreen.Dark);

        // Darkened twice from the handoff's #0E8A4F: once so white on it clears 4.5:1, and again so
        // the accent works as text on the light ground. See AccentPalette for the reasoning.
        Assert.Equal(Color.FromArgb(255, 0x0C, 0x75, 0x45), brandGreen.Light);
    }

    /// <summary>The window ground each theme paints, which accent text has to be readable on.</summary>
    /// <remarks>
    /// Copied from DsBgBrush in App.xaml. XAML resources cannot be read without a window, and the
    /// alternative to copying them is not checking, which is how these values went unchecked in the
    /// first place.
    /// </remarks>
    static Color Background(bool isDark) => isDark
        ? Color.FromArgb(255, 0x15, 0x1C, 0x26)
        : Color.FromArgb(255, 0xDC, 0xE5, 0xEF);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryAccentIsReadableAsTextOnTheWindowGround(bool isDark)
    {
        // The accent is not only a fill. It is the newer version in the preview sheet and the link
        // in the sidebar, both of them small text on the page background. The light values were
        // chosen to carry white text and nobody checked them the other way round: three of the four
        // were between 3.5:1 and 4.5:1 as text, which is unreadable at 11px and looks deliberate.
        foreach (var accent in AccentPalette.All)
        {
            var ratio = ContrastRatio(accent.ForTheme(isDark), Background(isDark));

            Assert.True(
                ratio >= 4.5,
                $"{accent.Id} as text in {(isDark ? "dark" : "light")} theme is only {ratio:F2}:1");
        }
    }
}
