using System.IO;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.Tests;

/// <summary>
/// Where a zip entry may be written to.
/// </summary>
/// <remarks>
/// The escape these guard against was reproduced rather than assumed: a zip written by .NET and
/// then patched so its central directory claims the entries were made on Unix comes back with
/// <c>Name = "..\..\evil.dll"</c> rather than <c>"evil.dll"</c>, passes an EndsWith(".dll") filter,
/// and resolves two directories above where the import meant to put it.
/// </remarks>
public class ZipEntryPathTests
{
    /// <summary>
    /// A real rooted folder for whichever host is running, because these tests run on ubuntu too.
    /// </summary>
    /// <remarks>
    /// It was a literal Windows path. On Linux that is not rooted, backslashes are ordinary
    /// characters, and Path.GetFullPath prepends the working directory - so every case here failed
    /// on the manifest sync workflow, which runs the core tests on ubuntu. The escapes being tested
    /// are Windows spellings on purpose: they are what a hostile zip actually contains, and
    /// ZipEntryPath now refuses them whatever the host thinks a separator is.
    /// </remarks>
    static readonly string Root = Path.Combine(Path.GetTempPath(), "dlss-swapper-tests", "import", "abc");

    [Theory]
    [InlineData("nvngx_dlss.dll")]
    [InlineData("sub/nvngx_dlss.dll")]
    [InlineData(@"sub\nvngx_dlss.dll")]
    public void AnEntryInsideTheFolderIsAllowed(string entryName)
    {
        Assert.True(ZipEntryPath.TryResolve(Root, entryName, out var fullPath));
        Assert.StartsWith(Root + Path.DirectorySeparatorChar, fullPath, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The reproduced case, in both separator styles.</summary>
    [Theory]
    [InlineData(@"..\..\evil.dll")]
    [InlineData("../../evil.dll")]
    [InlineData(@"..\evil.dll")]
    [InlineData(@"sub\..\..\evil.dll")]
    public void AnEntryThatClimbsOutIsRefused(string entryName)
    {
        Assert.False(ZipEntryPath.TryResolve(Root, entryName, out var fullPath));
        Assert.Equal(string.Empty, fullPath);
    }

    /// <summary>
    /// Path.Combine discards the folder entirely when the second argument is rooted, so this one
    /// does not even look like an escape - it simply lands wherever the archive said.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Windows\System32\evil.dll")]
    [InlineData(@"\\server\share\evil.dll")]
    [InlineData(@"\evil.dll")]
    public void ARootedEntryIsRefused(string entryName)
    {
        Assert.False(ZipEntryPath.TryResolve(Root, entryName, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sub/")]
    [InlineData(@"sub\")]
    public void AnEntryWithNothingToExtractIsRefused(string entryName)
    {
        Assert.False(ZipEntryPath.TryResolve(Root, entryName, out _));
    }

    /// <summary>
    /// A sibling whose name merely starts with the root's is outside it.
    /// </summary>
    [Fact]
    public void AFolderThatOnlySharesAPrefixIsNotInside()
    {
        Assert.False(ZipEntryPath.TryResolve(Root, @"..\abc_backup\evil.dll", out _));
    }
}
