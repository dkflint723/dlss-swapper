using System.Linq;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers how a long list of dll versions is split into release lines.
/// </summary>
/// <remarks>
/// One engine can carry over a hundred versions. Grouped wrongly they are worse than ungrouped,
/// because a heading that does not describe what is under it is read as fact.
/// </remarks>
public class DllVersionLineTests
{
    [Theory]
    [InlineData("310.7", "310")]
    [InlineData("310.5.3", "310")]
    [InlineData("310", "310")]
    public void ADlssStyleMajorIsTheWholeLine(string version, string expected)
    {
        // DLSS numbers its current line 310.x, so the major on its own identifies it.
        Assert.Equal(expected, DllVersionLine.KeyFor(version));
    }

    [Theory]
    [InlineData("3.8.10", "3.8")]
    [InlineData("3.7.20", "3.7")]
    [InlineData("2.0.1", "2.0")]
    public void ASingleDigitMajorNeedsTheMinorToo(string version, string expected)
    {
        // Without the minor, every FSR 3.x and XeSS 2.x version collapses into one group called
        // "3" or "2", which is the same wall of numbers the grouping exists to break up.
        Assert.Equal(expected, DllVersionLine.KeyFor(version));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("preview")]
    public void AVersionThatCannotBeReadGetsNoLine(string version)
    {
        // A manifest entry with an odd version must not take the page down or invent a heading.
        Assert.Equal(string.Empty, DllVersionLine.KeyFor(version));
    }

    [Fact]
    public void TheNewestLinesStaySeparateAndTheRestRollUp()
    {
        var versions = new[]
        {
            "310.7", "310.6", "310.1",   // line 310
            "3.8.10", "3.8",             // line 3.8
            "3.7.20", "3.7",             // line 3.7
            "3.6", "3.5.10", "3.1.30",   // the tail
        };

        var lines = DllVersionLine.AssignLines(versions);

        Assert.Equal(new[] { "310", "310", "310" }, lines.Take(3));
        Assert.Equal(new[] { "3.8", "3.8" }, lines.Skip(3).Take(2));
        Assert.Equal(new[] { "3.7", "3.7" }, lines.Skip(5).Take(2));

        // Everything past the third line shares one heading, named after the newest of them.
        Assert.Equal(new[] { "3.6", "3.6", "3.6" }, lines.Skip(7));
    }

    [Fact]
    public void EveryVersionGetsExactlyOneLine()
    {
        var versions = new[] { "310.7", "3.8", "3.7", "3.6", "3.5", "3.1", "2.5" };

        var lines = DllVersionLine.AssignLines(versions);

        Assert.Equal(versions.Length, lines.Count);
        Assert.All(lines, x => Assert.False(string.IsNullOrEmpty(x)));
    }

    [Fact]
    public void AShortListIsNeverRolledUp()
    {
        // Three lines or fewer all stand on their own; there is no tail to name.
        var lines = DllVersionLine.AssignLines(new[] { "310.7", "3.8", "3.7" });

        Assert.Equal(new[] { "310", "3.8", "3.7" }, lines);
    }

    [Fact]
    public void TheRolledUpHeadingSaysItCoversMore()
    {
        var plain = DllVersionLine.Label("DLSS", "310", isRolledUp: false);
        var rolled = DllVersionLine.Label("DLSS", "3.6", isRolledUp: true);

        Assert.Equal("DLSS 310", plain);
        Assert.NotEqual("DLSS 3.6", rolled);
        Assert.Contains("3.6", rolled);
        Assert.DoesNotContain("LangResourceError", rolled);
    }
}
