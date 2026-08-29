using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DLSS_Swapper.Helpers;
using ValveKeyValue;

namespace DLSS_Swapper.Data.Steam;

/// <summary>
/// A game somebody added to Steam themselves, rather than one Steam installed.
/// </summary>
/// <param name="AppId">
/// The number Steam gives the shortcut, which is what its library page is keyed by.
/// </param>
/// <param name="Name">The name as it reads in Steam.</param>
/// <param name="InstallPath">The folder to look in, already checked for existence and sanity.</param>
internal sealed record SteamShortcut(uint AppId, string Name, string InstallPath);

/// <summary>
/// Reads the non-Steam games somebody has added to Steam.
/// </summary>
/// <remarks>
/// <para>
/// Steam keeps these in <c>userdata/&lt;account&gt;/config/shortcuts.vdf</c>, which is binary
/// key values rather than the text kind the app manifests use - so it is read with the binary
/// serializer, not a second parser.
/// </para>
/// <para>
/// They are worth reading because a shortcut is how anything not bought on Steam gets played
/// through Steam, and those games have the same dlls in them as any other. Steam itself will
/// never write an <c>appmanifest_*.acf</c> for one, so the library scan that reads those cannot
/// see them at all.
/// </para>
/// <para>
/// Everything here treats the file as something written by other software that may be older,
/// newer, or half written: a shortcut that cannot be read is skipped rather than raised, because
/// one bad entry should cost that entry and not the scan.
/// </para>
/// </remarks>
internal static class SteamShortcuts
{
    /// <summary>
    /// Every shortcut across every account signed in on this machine.
    /// </summary>
    /// <remarks>
    /// Keyed by app id so that two accounts with the same game added produce one game rather than
    /// two, since the id is what the library page and this app both key on.
    /// </remarks>
    public static List<SteamShortcut> Load()
    {
        var shortcuts = new Dictionary<uint, SteamShortcut>();

        var installPath = SteamLibrary.GetInstallPath();
        if (string.IsNullOrEmpty(installPath))
        {
            return new List<SteamShortcut>();
        }

        var userDataPath = Path.Combine(installPath, "userdata");
        if (Directory.Exists(userDataPath) == false)
        {
            return new List<SteamShortcut>();
        }

        string[] accountPaths;
        try
        {
            accountPaths = Directory.GetDirectories(userDataPath);
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Could not list Steam accounts in {userDataPath}");
            return new List<SteamShortcut>();
        }

        foreach (var accountPath in accountPaths)
        {
            var shortcutsFile = Path.Combine(accountPath, "config", "shortcuts.vdf");
            if (File.Exists(shortcutsFile) == false)
            {
                continue;
            }

            foreach (var shortcut in LoadFile(shortcutsFile))
            {
                // First account to name an id wins. Nothing distinguishes two accounts' copies of
                // one shortcut, so there is nothing to choose between them.
                if (shortcuts.ContainsKey(shortcut.AppId) == false)
                {
                    shortcuts[shortcut.AppId] = shortcut;
                }
            }
        }

        return shortcuts.Values.ToList();
    }

    /// <summary>
    /// The readable shortcuts in one <c>shortcuts.vdf</c>.
    /// </summary>
    internal static List<SteamShortcut> LoadFile(string shortcutsFile)
    {
        var shortcuts = new List<SteamShortcut>();

        try
        {
            var kvSerializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Binary);

            using (var fileStream = File.OpenRead(shortcutsFile))
            {
                var document = kvSerializer.Deserialize(fileStream);

                // The root is a map whose keys are "0", "1", "2" and whose values are the
                // shortcuts. The keys carry nothing but order, so only the values are read.
                foreach (var entry in document.Root.Children)
                {
                    var shortcut = ReadShortcut(entry.Value);
                    if (shortcut is not null)
                    {
                        shortcuts.Add(shortcut);
                    }
                }
            }
        }
        catch (Exception err)
        {
            // A file being written while it is read, or a format that has moved on. Neither is
            // worth failing a library scan over.
            Logger.Error(err, $"Could not read Steam shortcuts from {shortcutsFile}");
        }

        return shortcuts;
    }

    /// <summary>
    /// One entry, or null when it is not something that can be scanned.
    /// </summary>
    static SteamShortcut? ReadShortcut(KVObject entry)
    {
        if (TryReadAppId(entry, out var appId) == false)
        {
            return null;
        }

        var name = FindValue(entry, "AppName") ?? FindValue(entry, "appname") ?? string.Empty;
        name = Unquote(name);

        var installPath = ResolveInstallPath(FindValue(entry, "StartDir"), FindValue(entry, "Exe"));
        if (installPath is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            // Better than an unnamed row. The folder is what somebody would recognise it by.
            name = Path.GetFileName(installPath);
        }

        return new SteamShortcut(appId, name, installPath);
    }

    /// <summary>
    /// The app id, read as unsigned.
    /// </summary>
    /// <remarks>
    /// The file stores it as a signed 32 bit integer and Steam uses the unsigned reading of the
    /// same bits everywhere it names the shortcut - the grid art beside this very file is written
    /// as <c>&lt;unsigned&gt;p.png</c>, and the library page reports the unsigned value. Reading it
    /// signed would produce a negative id that matches nothing.
    /// </remarks>
    internal static bool TryReadAppId(KVObject entry, out uint appId)
    {
        return TryParseAppId(FindValue(entry, "appid"), out appId);
    }

    /// <summary>
    /// An app id as written in the file, read as unsigned.
    /// </summary>
    internal static bool TryParseAppId(string? raw, out uint appId)
    {
        appId = 0;

        if (raw is null)
        {
            return false;
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) == false)
        {
            return false;
        }

        // Through int first so that both readings land on the same bits: a serializer handing back
        // -2067115218 and one handing back 2227852078 are describing the same shortcut.
        appId = unchecked((uint)(int)parsed);

        // Zero is what an entry that has never been given an id reads as, and it cannot be matched
        // to anything.
        return appId != 0;
    }

    /// <summary>
    /// A child's value by name, case insensitively.
    /// </summary>
    /// <remarks>
    /// Case insensitively because the keys are written by whatever added the shortcut, and the
    /// casing does vary between Steam's own writer and the tools people use to add games in bulk.
    /// </remarks>
    internal static string? FindValue(KVObject entry, string name)
    {
        foreach (var child in entry.Children)
        {
            if (string.Equals(child.Key, name, StringComparison.OrdinalIgnoreCase) == false)
            {
                continue;
            }

            // A nested map - the tags list is one - has no scalar reading, and asking for one
            // throws. None of the fields wanted here are maps.
            if (child.Value is null || child.Value.IsCollection)
            {
                return null;
            }

            return Convert.ToString(child.Value, CultureInfo.InvariantCulture);
        }

        return null;
    }

    /// <summary>
    /// The folder to scan for a shortcut, or null when there is not a sensible one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The start directory is preferred because it is the game's own folder, and is what Steam
    /// fills in when somebody browses to an executable. It is only fallen back on to the
    /// executable's folder when it is missing or no longer exists.
    /// </para>
    /// <para>
    /// Both are written by hand as often as by Steam, so this file has seen every shape: wrapped in
    /// quotes, with a trailing separator, and with the two separators mixed inside one path.
    /// </para>
    /// </remarks>
    internal static string? ResolveInstallPath(string? startDir, string? exe)
    {
        var fromStartDir = CleanDirectory(startDir);
        if (fromStartDir is not null && IsSafeToScan(fromStartDir))
        {
            return fromStartDir;
        }

        var executable = ExtractExecutablePath(exe);
        if (executable is null)
        {
            return null;
        }

        string? directory;
        try
        {
            directory = Path.GetDirectoryName(executable);
        }
        catch (Exception)
        {
            return null;
        }

        var fromExe = CleanDirectory(directory);
        if (fromExe is not null && IsSafeToScan(fromExe))
        {
            return fromExe;
        }

        return null;
    }

    /// <summary>
    /// A directory as written in the file, made into one this app can use, or null.
    /// </summary>
    internal static string? CleanDirectory(string? value)
    {
        var cleaned = Unquote(value ?? string.Empty);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        try
        {
            // Settles the mixed separators and the trailing one in the same call the rest of the
            // app uses, so a shortcut's path compares equal to the same folder found any other way.
            return PathHelpers.NormalizePath(cleaned);
        }
        catch (Exception)
        {
            // Not a path this platform can express. Nothing to scan.
            return null;
        }
    }

    /// <summary>
    /// The executable out of an Exe field, which carries its arguments too.
    /// </summary>
    /// <remarks>
    /// Quoted when it has a space in it, which is most of them, and followed by whatever arguments
    /// the shortcut was given - a launcher script's whole command line, in the worst case seen.
    /// </remarks>
    internal static string? ExtractExecutablePath(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe))
        {
            return null;
        }

        var trimmed = exe.Trim();

        if (trimmed.StartsWith('"'))
        {
            var closing = trimmed.IndexOf('"', 1);
            if (closing > 1)
            {
                return trimmed.Substring(1, closing - 1);
            }

            return null;
        }

        // Unquoted, so it cannot contain a space - anything after one is an argument.
        var firstSpace = trimmed.IndexOf(' ');
        return firstSpace < 0 ? trimmed : trimmed.Substring(0, firstSpace);
    }

    /// <summary>
    /// Strips a wrapping pair of quotes, which several of these fields arrive with.
    /// </summary>
    internal static string Unquote(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
        }

        return trimmed;
    }

    /// <summary>
    /// Whether a folder is a game folder rather than something that must never be walked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the guard that matters most in this file. A shortcut's start directory can be
    /// anything somebody typed, and the fallback is the folder of its executable - which for a
    /// shortcut that launches a script is <c>C:\Windows\System32</c>, because the executable is
    /// cmd. Processing a game walks its install path for <c>*.dll</c> with subdirectories
    /// included, so accepting one of those would set the app walking the entire system directory,
    /// and finding real upscaler dlls in there that belong to no game and must not be swapped.
    /// </para>
    /// <para>
    /// So a drive root, the Windows directory, and the well known folders that hold many programs
    /// rather than being one are refused. A folder inside any of them is fine - plenty of games
    /// live under Program Files - it is only these containers themselves that are wrong.
    /// </para>
    /// </remarks>
    internal static bool IsSafeToScan(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (Directory.Exists(path) == false)
        {
            return false;
        }

        try
        {
            var full = PathHelpers.NormalizePath(path);

            // A drive root, which is every game on that drive and every system folder with it.
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root) == false
                && string.Equals(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // The Windows directory, and anything within it. This one is refused by prefix rather
            // than by equality because a shortcut pointing at System32 is the case that prompted
            // all of this, and that is not the Windows folder itself.
            var windows = SafeFolder(Environment.SpecialFolder.Windows);
            if (windows is not null && IsWithin(full, windows))
            {
                return false;
            }

            // Containers: each holds many programs, so scanning one is scanning all of them.
            foreach (var folder in new[]
            {
                Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86,
                Environment.SpecialFolder.CommonProgramFiles,
                Environment.SpecialFolder.CommonProgramFilesX86,
                Environment.SpecialFolder.CommonApplicationData,
                Environment.SpecialFolder.UserProfile,
                Environment.SpecialFolder.Desktop,
                Environment.SpecialFolder.DesktopDirectory,
                Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolder.System,
                Environment.SpecialFolder.SystemX86,
            })
            {
                var special = SafeFolder(folder);
                if (special is not null && string.Equals(full, special, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// A special folder, normalized, or null when this machine does not have one.
    /// </summary>
    static string? SafeFolder(Environment.SpecialFolder folder)
    {
        try
        {
            var path = Environment.GetFolderPath(folder);
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return PathHelpers.NormalizePath(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether one path is the same folder as another, or sits inside it.
    /// </summary>
    /// <remarks>
    /// Compared with a separator appended so that a sibling whose name merely starts the same way -
    /// Windows.old beside Windows - is not read as being inside it.
    /// </remarks>
    internal static bool IsWithin(string path, string container)
    {
        if (string.Equals(path, container, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = container.EndsWith(Path.DirectorySeparatorChar)
            ? container
            : container + Path.DirectorySeparatorChar;

        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
