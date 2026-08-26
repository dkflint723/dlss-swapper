using System;
using System.IO;

namespace DLSS_Swapper.Data;

/// <summary>
/// Where a zip entry is allowed to be written to.
/// </summary>
/// <remarks>
/// <para>
/// A zip is a file somebody was given, and the names inside it are theirs to choose. Importing one
/// used to build its destination as <c>Path.Combine(tempFolder, entry.Name)</c> and extract, on the
/// understanding that <c>ZipArchiveEntry.Name</c> is only ever a bare file name.
/// </para>
/// <para>
/// It is not. .NET decides how to split an entry name using the archive's "version made by" byte:
/// for an entry flagged as made on Unix it splits on '/' alone, so a name of
/// <c>..\..\evil.dll</c> survives into <c>Name</c> intact. Measured rather than reasoned about - a
/// zip written by .NET and then patched at those two bytes gives
/// <c>Name = "..\..\evil.dll"</c>, which passes an <c>EndsWith(".dll")</c> filter and resolves two
/// directories above where it was meant to go. A rooted name such as <c>C:\somewhere\evil.dll</c>
/// is worse still, because <c>Path.Combine</c> discards everything before it.
/// </para>
/// <para>
/// The damage is a write to an arbitrary user-writable path, at a depth the archive chooses. In
/// this app's import the extracted file is deleted again afterwards, so the harm is destroying
/// somebody's file rather than planting one - which is not much of a consolation.
/// </para>
/// </remarks>
public static class ZipEntryPath
{
    /// <summary>
    /// Resolves where an entry should be written, refusing anything that leaves the folder.
    /// </summary>
    /// <param name="destinationDirectory">The folder the entry must end up inside.</param>
    /// <param name="entryName">The name as it appears in the archive, untrusted.</param>
    /// <param name="fullPath">The resolved absolute path, when the answer is yes.</param>
    /// <returns>Whether the entry may be written.</returns>
    /// <remarks>
    /// Containment is decided on the resolved paths rather than on the text of the name, so it does
    /// not depend on having thought of every way to spell "up one level".
    /// </remarks>
    public static bool TryResolve(string destinationDirectory, string entryName, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(destinationDirectory) || string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        // A directory entry, which has nothing to extract.
        if (entryName.EndsWith('/') || entryName.EndsWith('\\'))
        {
            return false;
        }

        // Both separators, on every host. A zip may spell its paths either way whatever machine
        // wrote it, and what counts as a separator otherwise depends on the machine reading it -
        // on Linux a backslash is an ordinary character, so "..\..\evil.dll" reads as one long
        // file name and sails through a containment check that only understands "/". This app only
        // ships on Windows, but the rule is not allowed to be true only there: a guard that stops
        // being a guard when the host changes is worse than no guard, because the tests that prove
        // it still pass somewhere.
        var normalisedEntry = entryName.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        // Rooted in the Windows sense as well as the host's. Path.IsPathRooted answers for the
        // platform it is running on, so a drive letter or a UNC path is "not rooted" on Linux and
        // would be combined onto the folder rather than refused.
        if (IsRootedAnywhere(normalisedEntry))
        {
            return false;
        }

        string resolvedRoot;
        string candidate;

        try
        {
            resolvedRoot = Path.GetFullPath(destinationDirectory);

            candidate = Path.GetFullPath(Path.Combine(resolvedRoot, normalisedEntry));
        }
        catch (Exception)
        {
            // An unparseable name - illegal characters, a path beyond what the platform allows - is
            // a refusal rather than something to throw out of an import loop.
            return false;
        }

        var rootWithSeparator = resolvedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? resolvedRoot
            : resolvedRoot + Path.DirectorySeparatorChar;

        // StartsWith on the separated root, so a sibling folder whose name merely begins with the
        // root's - "root_backup" next to "root" - is not mistaken for being inside it.
        if (candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) == false)
        {
            return false;
        }

        fullPath = candidate;

        return true;
    }

    /// <summary>
    /// Whether this name names an absolute location, by this platform's rules or by Windows'.
    /// </summary>
    /// <remarks>
    /// Path.Combine silently discards the folder when the second argument is rooted, so an absolute
    /// entry name does not even look like an escape - it simply lands wherever it says. Windows
    /// spellings are checked explicitly rather than left to Path.IsPathRooted, which answers for
    /// whichever platform happens to be running.
    /// </remarks>
    static bool IsRootedAnywhere(string entryName)
    {
        if (Path.IsPathRooted(entryName))
        {
            return true;
        }

        // A leading separator: "\evil.dll", and UNC "\\server\share\evil.dll".
        if (entryName[0] == Path.DirectorySeparatorChar || entryName[0] == Path.AltDirectorySeparatorChar)
        {
            return true;
        }

        // A drive letter: "C:\evil.dll", and the drive relative "C:evil.dll", which is just as
        // much not inside the folder we asked for.
        return entryName.Length >= 2
            && entryName[1] == ':'
            && char.IsAsciiLetter(entryName[0]);
    }
}
