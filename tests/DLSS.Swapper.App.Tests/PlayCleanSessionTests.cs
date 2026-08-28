using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers how a play-clean session decides whether a process belongs to the game.
/// </summary>
/// <remarks>
/// Launching goes through a store and hands back no process, so "is the game running" is decided
/// entirely by whether an executable's path lives under the install folder. Too loose and someone
/// else's process keeps the watch alive forever; too strict and the restore fires while the game
/// is still up. Either mistake writes — or fails to write — into a game folder.
/// </remarks>
public class PlayCleanSessionTests
{
    [Theory]
    [InlineData(@"D:\Games\DOOM\doom.exe", @"D:\Games\DOOM", true)]
    [InlineData(@"D:\Games\DOOM\bin\x64\doom.exe", @"D:\Games\DOOM", true)]
    [InlineData(@"d:\games\doom\DOOM.EXE", @"D:\Games\DOOM", true)]
    [InlineData(@"D:\Games\DOOM\doom.exe", @"D:\Games\DOOM\", true)]
    public void AProcessInsideTheInstallFolderIsTheGame(string path, string root, bool expected)
    {
        Assert.Equal(expected, PlayCleanSession.IsPathUnder(path, root));
    }

    [Theory]
    [InlineData(@"D:\Games\DOOM Eternal\doom.exe", @"D:\Games\DOOM")]
    [InlineData(@"D:\Games\DOOM", @"D:\Games\DOOM")]
    [InlineData(@"C:\Windows\System32\svchost.exe", @"D:\Games\DOOM")]
    [InlineData(@"D:\Games\doomlauncher.exe", @"D:\Games\DOOM")]
    public void AProcessOutsideItIsNot(string path, string root)
    {
        // The sibling-folder case is the trap: "DOOM Eternal" starts with "DOOM" as a string, and
        // a prefix check without the separator would watch the wrong game's whole install.
        Assert.False(PlayCleanSession.IsPathUnder(path, root));
    }

    [Theory]
    [InlineData("", @"D:\Games\DOOM")]
    [InlineData(@"D:\Games\DOOM\doom.exe", "")]
    public void MissingPathsMatchNothing(string path, string root)
    {
        Assert.False(PlayCleanSession.IsPathUnder(path, root));
    }
}
