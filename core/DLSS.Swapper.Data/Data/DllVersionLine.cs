using System.Collections.Generic;
using System.Globalization;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>
/// Which release line a dll version belongs to, for grouping a long list of them.
/// </summary>
/// <remarks>
/// Derived from the version rather than stored, because nothing in the manifest says what a
/// "line" is. The grouping exists because one engine can have over a hundred versions: as a flat
/// run they are a wall of near identical numbers, and the only question most people bring to the
/// page is whether they are near the top of it.
/// </remarks>
public static class DllVersionLine
{
    /// <summary>
    /// How many lines are shown separately before the rest are rolled into one.
    /// </summary>
    /// <remarks>
    /// The newest few are the ones anyone chooses between. Older lines still need to be reachable,
    /// which is why they are rolled up rather than hidden, but they do not each deserve a heading.
    /// </remarks>
    public const int SeparateLines = 3;

    /// <summary>
    /// The line a version belongs to, as a sortable key.
    /// </summary>
    /// <remarks>
    /// DLSS numbers its current line 310.x, so the major alone is the line there. Everything on a
    /// single digit major, like FSR 3.1 or XeSS 2.0, needs the minor too or every version collapses
    /// into one group called "3".
    /// </remarks>
    public static string KeyFor(string displayVersion)
    {
        var parts = (displayVersion ?? string.Empty).Split('.');
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return string.Empty;
        }

        if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major) == false)
        {
            return string.Empty;
        }

        if (major >= 100 || parts.Length == 1)
        {
            return parts[0];
        }

        return $"{parts[0]}.{parts[1]}";
    }

    /// <summary>Reads as "DLSS 310", or "DLSS 3.5 and older" for the rolled up tail.</summary>
    public static string Label(string engineName, string lineKey, bool isRolledUp)
    {
        var line = string.IsNullOrEmpty(lineKey)
            ? engineName
            : $"{engineName} {lineKey}";

        return isRolledUp
            ? ResourceHelper.GetFormattedResourceTemplate("Upscalers_VersionLineAndOlderTemplate", line)
            : line;
    }

    /// <summary>
    /// Splits an ordered list of versions into lines, rolling the tail into one group.
    /// </summary>
    /// <param name="orderedVersions">Display versions, newest first, in the order the page shows them.</param>
    /// <returns>The line key for each version, in the same order, with the tail sharing one key.</returns>
    /// <remarks>
    /// Takes the order it is given rather than sorting again. The page already ranks these, and a
    /// second opinion about which version is newest is exactly how a list ends up disagreeing with
    /// its own headings.
    /// </remarks>
    public static IReadOnlyList<string> AssignLines(IReadOnlyList<string> orderedVersions)
    {
        var assigned = new List<string>(orderedVersions.Count);
        var seen = new List<string>();
        var rolledUpKey = (string?)null;

        foreach (var version in orderedVersions)
        {
            var key = KeyFor(version);

            if (rolledUpKey is not null)
            {
                assigned.Add(rolledUpKey);
                continue;
            }

            if (seen.Contains(key) == false)
            {
                if (seen.Count == SeparateLines)
                {
                    // This line begins the tail, and names it.
                    rolledUpKey = key;
                    assigned.Add(key);
                    continue;
                }

                seen.Add(key);
            }

            assigned.Add(key);
        }

        return assigned;
    }
}
