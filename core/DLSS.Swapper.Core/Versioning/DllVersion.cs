using System;
using System.Globalization;

namespace DLSS_Swapper.Versioning;

/// <summary>
/// Converts a dll version string into the same packed number the dll manifest uses, so an installed
/// dll can be compared against the versions available for download.
/// </summary>
/// <remarks>
/// The manifest packs a four part version into a ulong as
/// <c>(major &lt;&lt; 48) | (minor &lt;&lt; 32) | (build &lt;&lt; 16) | revision</c>. Anything read back
/// off disk has to use that exact layout or comparisons silently give the wrong answer, which is why
/// this lives here with tests rather than inline at the call site.
/// </remarks>
public static class DllVersion
{
    /// <summary>
    /// Parses a version such as "310.7.0.0" into its packed form. Missing trailing components are
    /// treated as zero, so "2.5" and "2.5.0.0" produce the same number.
    /// </summary>
    /// <remarks>
    /// Accepts commas as well as dots. FileVersionInfo reports the version using the current
    /// culture's separator, so the same dll can come back as "310,7,0,0" on some machines.
    /// </remarks>
    public static bool TryParse(string? version, out ulong versionNumber)
    {
        versionNumber = 0;

        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var parts = version.Split(['.', ','], StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Length > 4)
        {
            return false;
        }

        ulong packed = 0;

        for (var i = 0; i < 4; i++)
        {
            ushort part = 0;

            if (i < parts.Length && ushort.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out part) == false)
            {
                return false;
            }

            packed |= (ulong)part << (48 - (i * 16));
        }

        versionNumber = packed;
        return true;
    }
}
