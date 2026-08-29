using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DLSS_Swapper.Data.Steam;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Reading the games somebody added to Steam themselves.
/// </summary>
/// <remarks>
/// <para>
/// The shapes tested here are the ones a real shortcuts.vdf on a real machine turned out to hold,
/// not invented ones: a path with a trailing separator, a path written with the other separator,
/// a path wrapped in quotes with both separators mixed inside it, and an entry whose executable is
/// cmd because the shortcut launches a script.
/// </para>
/// <para>
/// That last one is why the safety rules exist. Processing a game walks its install path for dlls
/// with subdirectories included, so a shortcut resolving to the system directory would set the app
/// walking all of Windows and finding upscaler dlls in there that belong to no game.
/// </para>
/// </remarks>
public class SteamShortcutsTests
{
    // ---------------------------------------------------------------- app ids

    /// <summary>
    /// The file stores the id signed; everything Steam names it with is unsigned.
    /// </summary>
    /// <remarks>
    /// Taken from a real file: the shortcut stored as -2067115218 has its grid art written beside
    /// it as 2227852078p.png, by Steam. Reading it signed produces an id that matches nothing.
    /// </remarks>
    [Theory]
    [InlineData("-2067115218", 2227852078u)]
    [InlineData("-254880136", 4040087160u)]
    [InlineData("-246118299", 4048848997u)]
    public void ASignedAppIdIsReadAsTheUnsignedOneSteamUses(string raw, uint expected)
    {
        Assert.True(SteamShortcuts.TryParseAppId(raw, out var appId));
        Assert.Equal(expected, appId);
    }

    /// <summary>
    /// A serializer that already hands back the unsigned reading lands on the same id.
    /// </summary>
    [Theory]
    [InlineData("2227852078", 2227852078u)]
    [InlineData("4040087160", 4040087160u)]
    public void AnAlreadyUnsignedAppIdIsUnchanged(string raw, uint expected)
    {
        Assert.True(SteamShortcuts.TryParseAppId(raw, out var appId));
        Assert.Equal(expected, appId);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("not a number")]
    public void AnAppIdThatCannotBeMatchedIsRefused(string raw)
    {
        Assert.False(SteamShortcuts.TryParseAppId(raw, out _));
    }

    // --------------------------------------------------------------- unquoting

    [Theory]
    [InlineData("\"D:\\Games\\Thing\"", "D:\\Games\\Thing")]
    [InlineData("  \"D:\\Games\\Thing\"  ", "D:\\Games\\Thing")]
    [InlineData("D:\\Games\\Thing", "D:\\Games\\Thing")]
    [InlineData("", "")]
    public void WrappingQuotesComeOff(string value, string expected)
    {
        Assert.Equal(expected, SteamShortcuts.Unquote(value));
    }

    // ------------------------------------------------------------- executables

    /// <summary>
    /// The Exe field carries the arguments too, so the executable has to be lifted out of it.
    /// </summary>
    [Theory]
    // The ordinary case, quoted because the path has a space in it.
    [InlineData("\"D:\\7th Heaven\\7th Heaven.exe\"", "D:\\7th Heaven\\7th Heaven.exe")]
    // A launcher script: the executable is cmd, and everything after it is its command line. This
    // is the entry that would send a scan into the system directory if its folder were ever used.
    [InlineData("\"C:\\Windows\\System32\\cmd.exe\" /k start /min \"Loading\" \"x.ps1\"", "C:\\Windows\\System32\\cmd.exe")]
    [InlineData("D:\\Games\\game.exe", "D:\\Games\\game.exe")]
    [InlineData("D:\\Games\\game.exe -windowed", "D:\\Games\\game.exe")]
    public void TheExecutableIsLiftedOutOfItsCommandLine(string exe, string expected)
    {
        Assert.Equal(expected, SteamShortcuts.ExtractExecutablePath(exe));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"unterminated")]
    public void AnExecutableThatCannotBeReadIsRefused(string exe)
    {
        Assert.Null(SteamShortcuts.ExtractExecutablePath(exe));
    }

    // -------------------------------------------------------------------- paths

    /// <summary>
    /// Every way a directory turned out to be written in a real file reaches the same folder.
    /// </summary>
    [Fact]
    public void TheWaysADirectoryIsWrittenAllReachTheSameFolder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dlss-swapper-tests", "SomeGame");
        Directory.CreateDirectory(directory);

        try
        {
            var expected = directory.TrimEnd(Path.DirectorySeparatorChar);

            // Plain.
            Assert.Equal(expected, SteamShortcuts.CleanDirectory(directory));

            // With a trailing separator, as the 7th Heaven entry had.
            Assert.Equal(expected, SteamShortcuts.CleanDirectory(directory + Path.DirectorySeparatorChar));

            // Wrapped in quotes, as the EmulationStation entry had.
            Assert.Equal(expected, SteamShortcuts.CleanDirectory("\"" + directory + "\""));

            // Written with the other separator, as the chiaki entry had.
            Assert.Equal(expected, SteamShortcuts.CleanDirectory(directory.Replace('\\', '/')));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ADirectoryThatIsNotOneIsRefused(string value)
    {
        Assert.Null(SteamShortcuts.CleanDirectory(value));
    }

    // ------------------------------------------------------------------- safety

    /// <summary>
    /// The folders that must never be walked.
    /// </summary>
    /// <remarks>
    /// Each of these holds many programs rather than being one, so scanning it is scanning all of
    /// them - and in the system directory's case, finding upscaler dlls that belong to no game.
    /// </remarks>
    [Fact]
    public void TheFoldersThatMustNeverBeWalkedAreRefused()
    {
        var refused = new List<string?>
        {
            Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        foreach (var path in refused)
        {
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path) == false)
            {
                continue;
            }

            Assert.False(SteamShortcuts.IsSafeToScan(path), $"{path} should never be walked for dlls.");
        }
    }

    /// <summary>
    /// A game that lives under one of those containers is still a game.
    /// </summary>
    /// <remarks>
    /// The rule is about the containers themselves, not what is inside them - a real shortcut on
    /// the machine this was written against points at Program Files\chiaki-ng.
    /// </remarks>
    [Fact]
    public void AFolderInsideAContainerIsStillScannable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dlss-swapper-tests", "InsideAContainer");
        Directory.CreateDirectory(directory);

        try
        {
            Assert.True(SteamShortcuts.IsSafeToScan(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void AFolderThatIsNotThereIsRefused()
    {
        var missing = Path.Combine(Path.GetTempPath(), "dlss-swapper-tests", "does-not-exist-" + Guid.NewGuid().ToString("N"));

        Assert.False(SteamShortcuts.IsSafeToScan(missing));
    }

    /// <summary>
    /// A sibling whose name merely starts the same way is not inside it.
    /// </summary>
    [Fact]
    public void ASiblingIsNotMistakenForSomethingInside()
    {
        Assert.True(SteamShortcuts.IsWithin(@"C:\Windows\System32", @"C:\Windows"));
        Assert.True(SteamShortcuts.IsWithin(@"C:\Windows", @"C:\Windows"));
        Assert.False(SteamShortcuts.IsWithin(@"C:\Windows.old", @"C:\Windows"));
        Assert.False(SteamShortcuts.IsWithin(@"C:\WindowsApps", @"C:\Windows"));
    }

    // ------------------------------------------------------- resolving together

    /// <summary>
    /// The start directory is preferred, and the executable's folder is the fallback.
    /// </summary>
    [Fact]
    public void TheStartDirectoryWinsAndTheExecutableIsTheFallback()
    {
        var startDir = Path.Combine(Path.GetTempPath(), "dlss-swapper-tests", "StartDir");
        var exeDir = Path.Combine(Path.GetTempPath(), "dlss-swapper-tests", "ExeDir");
        Directory.CreateDirectory(startDir);
        Directory.CreateDirectory(exeDir);

        try
        {
            var exe = "\"" + Path.Combine(exeDir, "game.exe") + "\"";

            // Both usable: the start directory is the game's own folder.
            Assert.Equal(
                startDir.TrimEnd(Path.DirectorySeparatorChar),
                SteamShortcuts.ResolveInstallPath(startDir, exe));

            // No start directory: fall back to where the executable is.
            Assert.Equal(
                exeDir.TrimEnd(Path.DirectorySeparatorChar),
                SteamShortcuts.ResolveInstallPath(string.Empty, exe));

            // A start directory that no longer exists: same fallback.
            Assert.Equal(
                exeDir.TrimEnd(Path.DirectorySeparatorChar),
                SteamShortcuts.ResolveInstallPath(Path.Combine(startDir, "gone"), exe));
        }
        finally
        {
            Directory.Delete(startDir, true);
            Directory.Delete(exeDir, true);
        }
    }

    /// <summary>
    /// A shortcut that launches a script does not send the scan into the system directory.
    /// </summary>
    /// <remarks>
    /// This is the real entry, near enough: its executable is cmd and its start directory is the
    /// folder the script lives in. If the start directory were ever dropped, the fallback would be
    /// cmd's folder - and this asserts that is refused rather than walked.
    /// </remarks>
    [Fact]
    public void AShortcutThatLaunchesAScriptCannotReachTheSystemDirectory()
    {
        // This app only runs on Windows, so the system directory is always there to be refused.
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        Assert.False(string.IsNullOrWhiteSpace(system));

        var exe = "\"" + Path.Combine(system, "cmd.exe") + "\" /k start /min \"Loading\" \"launcher.ps1\"";

        // No usable start directory, so the only candidate left is cmd's own folder.
        Assert.Null(SteamShortcuts.ResolveInstallPath(string.Empty, exe));
        Assert.Null(SteamShortcuts.ResolveInstallPath("   ", exe));
    }

    // ------------------------------------------------------------ the whole file

    /// <summary>
    /// A whole shortcuts.vdf, built as Steam writes them, read back through the real parser.
    /// </summary>
    /// <remarks>
    /// Built here rather than committed as a fixture so that what is being asserted is visible, and
    /// so it carries no real person's paths. The bytes follow binary key values: 0x00 opens a map,
    /// 0x01 a string, 0x02 a 32 bit integer, and 0x08 closes a map.
    /// </remarks>
    [Fact]
    public void AWholeShortcutsFileIsRead()
    {
        var gameDirectory = Path.Combine(Path.GetTempPath(), "dlss-swapper-tests", "WholeFile");
        Directory.CreateDirectory(gameDirectory);

        var shortcutsFile = Path.Combine(gameDirectory, "shortcuts.vdf");

        try
        {
            using (var stream = File.Create(shortcutsFile))
            {
                BeginMap(stream, "shortcuts");

                BeginMap(stream, "0");
                // Stored signed, as Steam writes it. 2227852078 unsigned.
                WriteInt32(stream, "appid", -2067115218);
                WriteString(stream, "AppName", "A Game");
                WriteString(stream, "Exe", "\"" + Path.Combine(gameDirectory, "game.exe") + "\"");
                WriteString(stream, "StartDir", gameDirectory + Path.DirectorySeparatorChar);
                // A nested map among the scalars, which the reader has to step over.
                BeginMap(stream, "tags");
                WriteString(stream, "0", "favorite");
                EndMap(stream);
                EndMap(stream);

                // An entry with no usable id, which should be skipped rather than fail the file.
                BeginMap(stream, "1");
                WriteInt32(stream, "appid", 0);
                WriteString(stream, "AppName", "Not Real");
                WriteString(stream, "StartDir", gameDirectory);
                EndMap(stream);

                EndMap(stream);

                // One more than closes the maps. A real file written by Steam ends with four of
                // these where three would balance the maps, and the serializer expects the extra
                // one - without it the whole document reads as empty rather than as an error.
                EndMap(stream);
            }

            var shortcuts = SteamShortcuts.LoadFile(shortcutsFile);

            var shortcut = Assert.Single(shortcuts);
            Assert.Equal(2227852078u, shortcut.AppId);
            Assert.Equal("A Game", shortcut.Name);
            Assert.Equal(gameDirectory.TrimEnd(Path.DirectorySeparatorChar), shortcut.InstallPath);
        }
        finally
        {
            Directory.Delete(gameDirectory, true);
        }
    }

    /// <summary>
    /// Something that is not a shortcuts file costs nothing but the entries in it.
    /// </summary>
    [Fact]
    public void AFileThatCannotBeReadYieldsNothingRatherThanThrowing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dlss-swapper-tests", "Garbage");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "shortcuts.vdf");

        try
        {
            File.WriteAllBytes(path, new byte[] { 0xFF, 0xFE, 0x42, 0x00, 0x99 });

            Assert.Empty(SteamShortcuts.LoadFile(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    static void BeginMap(Stream stream, string name)
    {
        stream.WriteByte(0x00);
        WriteCString(stream, name);
    }

    static void EndMap(Stream stream)
    {
        stream.WriteByte(0x08);
    }

    static void WriteString(Stream stream, string name, string value)
    {
        stream.WriteByte(0x01);
        WriteCString(stream, name);
        WriteCString(stream, value);
    }

    static void WriteInt32(Stream stream, string name, int value)
    {
        stream.WriteByte(0x02);
        WriteCString(stream, name);
        stream.Write(BitConverter.GetBytes(value), 0, 4);
    }

    static void WriteCString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
        stream.WriteByte(0x00);
    }
}
