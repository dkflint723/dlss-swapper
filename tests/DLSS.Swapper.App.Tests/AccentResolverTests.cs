using DLSS_Swapper.Data;
using Windows.UI;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers which accent wins and what stays readable on it.
/// </summary>
public class AccentResolverTests
{
    static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);

    [Fact]
    public void APresetResolvesToItsOwnDarkValueAndInk()
    {
        var brandGreen = AccentPalette.FromIndex(0);

        var resolved = AccentResolver.Resolve(0, isDarkTheme: true, desktopAccent: null);

        Assert.Equal(brandGreen.Dark, resolved.Accent);
        Assert.Equal(brandGreen.DarkInk, resolved.Ink);
    }

    [Fact]
    public void APresetResolvesToItsOwnLightValueAndInk()
    {
        var brandGreen = AccentPalette.FromIndex(0);

        var resolved = AccentResolver.Resolve(0, isDarkTheme: false, desktopAccent: null);

        Assert.Equal(brandGreen.Light, resolved.Accent);
        Assert.Equal(brandGreen.LightInk, resolved.Ink);
    }

    [Fact]
    public void TheDesktopAccentOverridesThePreset()
    {
        var desktop = Rgb(0xE3, 0x00, 0x8C);

        var resolved = AccentResolver.Resolve(0, isDarkTheme: true, desktopAccent: desktop);

        Assert.Equal(desktop, resolved.Accent);
    }

    [Fact]
    public void TheDesktopAccentOverridesInBothThemes()
    {
        var desktop = Rgb(0xE3, 0x00, 0x8C);

        Assert.Equal(desktop, AccentResolver.Resolve(2, isDarkTheme: true, desktop).Accent);
        Assert.Equal(desktop, AccentResolver.Resolve(2, isDarkTheme: false, desktop).Accent);
    }

    [Fact]
    public void AnOutOfRangePresetStillResolves()
    {
        // The index is persisted, so a settings file from another build must not leave the app with
        // no accent at all.
        var resolved = AccentResolver.Resolve(99, isDarkTheme: true, desktopAccent: null);

        Assert.Equal(AccentPalette.FromIndex(0).Dark, resolved.Accent);
    }

    [Fact]
    public void ADarkDesktopAccentGetsLightInk()
    {
        var resolved = AccentResolver.Resolve(0, isDarkTheme: true, desktopAccent: Rgb(0x10, 0x20, 0x60));

        Assert.Equal(Rgb(0xFF, 0xFF, 0xFF), resolved.Ink);
    }

    [Fact]
    public void ALightDesktopAccentGetsDarkInk()
    {
        var resolved = AccentResolver.Resolve(0, isDarkTheme: true, desktopAccent: Rgb(0xFF, 0xE0, 0x40));

        Assert.NotEqual(Rgb(0xFF, 0xFF, 0xFF), resolved.Ink);
    }

    [Theory]
    [InlineData(0x00, 0x00, 0x00)]
    [InlineData(0xFF, 0xFF, 0xFF)]
    [InlineData(0xE3, 0x00, 0x8C)]
    [InlineData(0x2E, 0xE0, 0x7A)]
    [InlineData(0x00, 0x67, 0xC0)]
    [InlineData(0xFF, 0xC8, 0x3D)]
    public void AnyDesktopAccentGetsReadableInk(byte r, byte g, byte b)
    {
        // The desktop accent is whatever the user picked in Windows, so the ink has to hold up
        // across the whole range rather than for the colours we happen to have tried.
        var accent = Rgb(r, g, b);
        var ink = AccentResolver.InkFor(accent);

        Assert.True(ContrastRatio(accent, ink) >= 4.5, $"#{r:X2}{g:X2}{b:X2} only reached {ContrastRatio(accent, ink):F2}:1");
    }

    [Theory]
    [InlineData(0x74, 0x74, 0x74)]
    [InlineData(0x77, 0x77, 0x77)]
    [InlineData(0x7A, 0x7A, 0x7A)]
    public void MidGreyCannotClearAaWithAnyInkSoTheBetterOneIsChosen(byte r, byte g, byte b)
    {
        // A narrow band of mid tones reaches neither 4.5:1 against white nor against near black —
        // #777777 manages 4.48 and 4.34. No ink choice fixes that, and the desktop accent is
        // whatever the user set in Windows, so the contract is "pick the better of the two", not
        // "always clear AA". Asserting the stronger promise would be asserting the impossible.
        var accent = Rgb(r, g, b);
        var chosen = AccentResolver.InkFor(accent);
        var rejected = chosen == Rgb(0xFF, 0xFF, 0xFF)
            ? Rgb(0x0C, 0x0D, 0x0F)
            : Rgb(0xFF, 0xFF, 0xFF);

        Assert.True(ContrastRatio(accent, chosen) >= ContrastRatio(accent, rejected));
    }

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
}
