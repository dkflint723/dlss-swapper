using System.IO;
using System.Text;
using System.Threading.Tasks;
using DLSS_Swapper;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// The three json files this app owns survive being interrupted, and a file that cannot be read is
/// never replaced on the strength of that.
/// </summary>
/// <remarks>
/// <para>
/// Each of settings.json, imported_manifest.json and releases.json was written by opening the live
/// file with FileMode.Create, which truncates it before a byte of the replacement is written.
/// Settings are rewritten on almost every toggle, so the window is not small, and the consequences
/// differed per file: settings were silently replaced with defaults, importing was disabled
/// permanently, and a truncated release cache threw out of an async void handler and closed the
/// window on every launch for thirty minutes.
/// </para>
/// <para>
/// These run against the real Storage, pointed at a throwaway folder.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public class DurableWriteTests
{
    [Fact]
    public async Task AnInterruptedWriteLeavesTheExistingFileAlone()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var path = Path.Combine(Storage.GetDynamicJsonFolder(), "probe.json");
        File.WriteAllText(path, "{\"the\":\"original\"}");

        // A write that gets part way and then fails, which is what a kill or a full disk looks like.
        var written = Storage.WriteFileAtomically(path, stream =>
        {
            stream.Write(Encoding.UTF8.GetBytes("{\"half\""));
            throw new IOException("the disk filled up");
        });

        Assert.False(written);
        Assert.Equal("{\"the\":\"original\"}", File.ReadAllText(path));
    }

    [Fact]
    public async Task AFailedWriteLeavesNoTemporaryFileBehind()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var path = Path.Combine(Storage.GetDynamicJsonFolder(), "probe.json");
        File.WriteAllText(path, "original");

        Storage.WriteFileAtomically(path, _ => throw new IOException("no"));

        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task AWriteThatSucceedsReplacesTheFileWhole()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var path = Path.Combine(Storage.GetDynamicJsonFolder(), "probe.json");
        File.WriteAllText(path, "original");

        var written = Storage.WriteFileAtomically(path, stream => stream.Write(Encoding.UTF8.GetBytes("replaced")));

        Assert.True(written);
        Assert.Equal("replaced", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    /// <summary>A .tmp left by an earlier crash must not stop the next save.</summary>
    [Fact]
    public async Task AStaleTemporaryFileDoesNotBlockTheNextWrite()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var path = Path.Combine(Storage.GetDynamicJsonFolder(), "probe.json");
        File.WriteAllText(path + ".tmp", "left over from last time");

        var written = Storage.WriteFileAtomically(path, stream => stream.Write(Encoding.UTF8.GetBytes("fresh")));

        Assert.True(written);
        Assert.Equal("fresh", File.ReadAllText(path));
    }

    [Fact]
    public async Task NoSettingsFileAtAllReadsAsAFirstRun()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var settingsFile = Path.Combine(Storage.GetDynamicJsonFolder(), "settings.json");
        if (File.Exists(settingsFile))
        {
            File.Delete(settingsFile);
        }

        var (outcome, settings) = Storage.LoadSettingsJson();

        Assert.Equal(Storage.SettingsLoadOutcome.Missing, outcome);
        Assert.Null(settings);
    }

    /// <summary>
    /// The distinction the whole read side rests on: a file that exists and is not settings is a
    /// different answer from no file, and both used to be reported as null.
    /// </summary>
    [Fact]
    public async Task AFileThatIsNotSettingsReadsAsCorruptRatherThanMissing()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var settingsFile = Path.Combine(Storage.GetDynamicJsonFolder(), "settings.json");
        File.WriteAllText(settingsFile, "{ this is not json");

        var (outcome, settings) = Storage.LoadSettingsJson();

        Assert.Equal(Storage.SettingsLoadOutcome.Corrupt, outcome);
        Assert.Null(settings);

        // And nothing has touched it in the course of finding that out.
        Assert.Equal("{ this is not json", File.ReadAllText(settingsFile));
    }

    /// <summary>
    /// A file held open by something else is very often a passing thing - an antivirus or a backup
    /// agent - so it must never be taken for a missing one and replaced.
    /// </summary>
    [Fact]
    public async Task ASettingsFileThatCannotBeOpenedReadsAsUnreadable()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var settingsFile = Path.Combine(Storage.GetDynamicJsonFolder(), "settings.json");
        File.WriteAllText(settingsFile, "{}");

        using (File.Open(settingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var (outcome, settings) = Storage.LoadSettingsJson();

            Assert.Equal(Storage.SettingsLoadOutcome.Unreadable, outcome);
            Assert.Null(settings);
        }

        Assert.Equal("{}", File.ReadAllText(settingsFile));
    }
}
