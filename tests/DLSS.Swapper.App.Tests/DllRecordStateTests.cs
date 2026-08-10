using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers what a dll version's row says about where the file is.
/// </summary>
/// <remarks>
/// This used to be readable only from which buttons the row had. State carried by iconography alone
/// is the thing this redesign removed from the games list, and it was still here.
/// </remarks>
public class DllRecordStateTests
{
    [Fact]
    public void EveryStateSaysSomething()
    {
        var onDisk = DllRecordState.Describe(isDownloaded: true, isImported: false);
        var imported = DllRecordState.Describe(isDownloaded: true, isImported: true);
        var missing = DllRecordState.Describe(isDownloaded: false, isImported: false);

        foreach (var text in new[] { onDisk, imported, missing })
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain("LangResourceError", text);
        }

        // Three states, three sentences. If any two collapse the column stops distinguishing them.
        Assert.NotEqual(onDisk, imported);
        Assert.NotEqual(onDisk, missing);
        Assert.NotEqual(imported, missing);
    }

    [Fact]
    public void ImportedBeatsOnDisk()
    {
        // An imported dll is on disk too. Where it came from is the more useful of the two, because
        // it is the one the manifest cannot vouch for.
        Assert.Equal(
            DllRecordState.Describe(isDownloaded: true, isImported: true),
            DllRecordState.Describe(isDownloaded: false, isImported: true));

        Assert.NotEqual(
            DllRecordState.Describe(isDownloaded: true, isImported: true),
            DllRecordState.Describe(isDownloaded: true, isImported: false));
    }

    [Fact]
    public void OnlyTheStatesWorthMarkingCarryAGlyph()
    {
        Assert.False(string.IsNullOrEmpty(DllRecordState.Glyph(isDownloaded: true, isImported: false)));
        Assert.False(string.IsNullOrEmpty(DllRecordState.Glyph(isDownloaded: true, isImported: true)));

        // Not downloaded is most of the page. Marking every one would make the absence of a mark
        // the exception rather than the rule.
        Assert.Empty(DllRecordState.Glyph(isDownloaded: false, isImported: false));
    }

    [Fact]
    public void OnDiskAndImportedAreToldApartByMoreThanColour()
    {
        // Neither the words nor the glyphs may be shared: the two states are distinguishable
        // without seeing a colour at all.
        Assert.NotEqual(
            DllRecordState.Glyph(isDownloaded: true, isImported: false),
            DllRecordState.Glyph(isDownloaded: true, isImported: true));
    }
}
