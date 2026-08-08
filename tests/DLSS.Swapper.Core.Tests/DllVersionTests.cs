using DLSS_Swapper.Versioning;
using Xunit;

namespace DLSS_Swapper.Tests;

public class DllVersionTests
{
    /// <summary>
    /// Expected values taken straight from static_manifest.json, so a change to the packing layout
    /// fails here rather than silently comparing installed dlls against the wrong numbers.
    /// </summary>
    [Theory]
    [InlineData("1.0.0.0", 281474976710656UL)]
    [InlineData("1.0.9.0", 281474977300480UL)]
    [InlineData("1.0.11.0", 281474977431552UL)]
    [InlineData("310.6.0.0", 87257268550107136UL)]
    [InlineData("310.7.0.0", 87257272845074432UL)]
    public void TryParse_MatchesTheManifestPacking(string version, ulong expected)
    {
        Assert.True(DllVersion.TryParse(version, out var actual));
        Assert.Equal(expected, actual);
    }

    /// <summary>FileVersionInfo reports the version using the current culture's separator.</summary>
    [Fact]
    public void TryParse_AcceptsCommaSeparatedVersions()
    {
        Assert.True(DllVersion.TryParse("310,7,0,0", out var comma));
        Assert.True(DllVersion.TryParse("310.7.0.0", out var dot));
        Assert.Equal(dot, comma);
    }

    [Fact]
    public void TryParse_TreatsMissingComponentsAsZero()
    {
        Assert.True(DllVersion.TryParse("2.5", out var shortForm));
        Assert.True(DllVersion.TryParse("2.5.0.0", out var longForm));
        Assert.Equal(longForm, shortForm);
    }

    [Fact]
    public void TryParse_OrdersVersionsCorrectly()
    {
        Assert.True(DllVersion.TryParse("310.7.0.0", out var newer));
        Assert.True(DllVersion.TryParse("310.6.0.0", out var older));
        Assert.True(DllVersion.TryParse("2.4.0.0", out var muchOlder));

        Assert.True(newer > older);
        Assert.True(older > muchOlder);
    }

    /// <summary>
    /// A build number must not be able to outrank a major version. This is the failure the packing
    /// exists to prevent, so it gets its own test.
    /// </summary>
    [Fact]
    public void TryParse_RanksMajorAboveLesserComponents()
    {
        Assert.True(DllVersion.TryParse("3.0.0.0", out var major));
        Assert.True(DllVersion.TryParse("2.99.999.999", out var lesser));

        Assert.True(major > lesser);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not.a.version")]
    [InlineData("1.2.3.4.5")]
    [InlineData("-1.0.0.0")]
    [InlineData("70000.0.0.0")]
    public void TryParse_RejectsUnusableInput(string? version)
    {
        Assert.False(DllVersion.TryParse(version, out var versionNumber));
        Assert.Equal(0UL, versionNumber);
    }
}
