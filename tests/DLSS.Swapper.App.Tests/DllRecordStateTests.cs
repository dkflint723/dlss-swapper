using System.Linq;
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
        var onDisk = DllRecordState.Describe(isDownloaded: true, isImported: false, isDownloading: false);
        var imported = DllRecordState.Describe(isDownloaded: true, isImported: true, isDownloading: false);
        var missing = DllRecordState.Describe(isDownloaded: false, isImported: false, isDownloading: false);
        var downloading = DllRecordState.Describe(isDownloaded: false, isImported: false, isDownloading: true);

        foreach (var text in new[] { onDisk, imported, missing, downloading })
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain("LangResourceError", text);
        }

        // Four states, four sentences. If any two collapse the column stops distinguishing them.
        var all = new[] { onDisk, imported, missing, downloading };
        Assert.Equal(all.Length, all.Distinct().Count());
    }

    [Fact]
    public void ImportedBeatsOnDisk()
    {
        // An imported dll is on disk too. Where it came from is the more useful of the two, because
        // it is the one the manifest cannot vouch for.
        Assert.Equal(
            DllRecordState.Describe(isDownloaded: true, isImported: true, isDownloading: false),
            DllRecordState.Describe(isDownloaded: false, isImported: true, isDownloading: false));

        Assert.NotEqual(
            DllRecordState.Describe(isDownloaded: true, isImported: true, isDownloading: false),
            DllRecordState.Describe(isDownloaded: true, isImported: false, isDownloading: false));
    }

    [Fact]
    public void DownloadingBeatsEverything()
    {
        // The row is mid-download, so whatever was true a moment ago is about to stop being true.
        // It read "Not downloaded" for the whole download, which is when that was least true.
        var downloading = DllRecordState.Describe(isDownloaded: false, isImported: false, isDownloading: true);

        Assert.Equal(downloading, DllRecordState.Describe(isDownloaded: true, isImported: false, isDownloading: true));
        Assert.Equal(downloading, DllRecordState.Describe(isDownloaded: true, isImported: true, isDownloading: true));
    }

    [Fact]
    public void OnlyTheStatesWorthMarkingCarryAGlyph()
    {
        Assert.False(string.IsNullOrEmpty(DllRecordState.Glyph(isDownloaded: true, isImported: false, isDownloading: false)));
        Assert.False(string.IsNullOrEmpty(DllRecordState.Glyph(isDownloaded: true, isImported: true, isDownloading: false)));

        // Not downloaded is most of the page. Marking every one would make the absence of a mark
        // the exception rather than the rule.
        Assert.Empty(DllRecordState.Glyph(isDownloaded: false, isImported: false, isDownloading: false));

        // Downloading has the bar along the row, which says more than a mark can.
        Assert.Empty(DllRecordState.Glyph(isDownloaded: false, isImported: false, isDownloading: true));
        Assert.Empty(DllRecordState.Glyph(isDownloaded: true, isImported: true, isDownloading: true));
    }

    [Fact]
    public void OnDiskAndImportedAreToldApartByMoreThanColour()
    {
        // Neither the words nor the glyphs may be shared: the two states are distinguishable
        // without seeing a colour at all.
        Assert.NotEqual(
            DllRecordState.Glyph(isDownloaded: true, isImported: false, isDownloading: false),
            DllRecordState.Glyph(isDownloaded: true, isImported: true, isDownloading: false));
    }
}
