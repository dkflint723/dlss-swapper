using System.Collections.Generic;

namespace DLSS_Swapper.Data;

/// <summary>
/// Whether a dll version survives the upscalers page's current narrowing.
/// </summary>
/// <remarks>
/// One rule for the list and for the count printed beside each engine's name, because those are the
/// same control: the number is on the button that opens the list. They were two rules before this
/// and they already disagreed — the count read the raw collection while the list hid debug files, so
/// DLSS said 107 over a list of 88.
/// </remarks>
public static class DllSearch
{
    /// <summary>Everything the page is currently filtering by, in the order it is cheapest to reject.</summary>
    public static bool Passes(DLLRecord record, string? query, bool allowDebugDlls)
    {
        if (allowDebugDlls == false && record.IsDevFile)
        {
            return false;
        }

        return Matches(record, query);
    }

    /// <summary>
    /// Whether a record matches what was typed.
    /// </summary>
    /// <remarks>
    /// Deliberately not the engine name: the engine is the column on the left, and typing "dlss"
    /// would match every row of every engine, which answers nothing.
    /// </remarks>
    public static bool Matches(DLLRecord record, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var trimmed = query.Trim();

        // A hash matches from its start only. Any two characters appear somewhere in nearly every
        // hash on the page, so a substring match would return the whole list and look broken.
        if (string.IsNullOrEmpty(record.MD5Hash) == false
            && record.MD5Hash.StartsWith(trimmed, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // DisplayName carries the version, the (v2) discriminator and the beta codenames, so it
        // answers most searches by itself. Version is separate because DisplayVersion drops
        // trailing zero groups, and "3.7.20.0" is what Windows shows in a file's properties.
        return Contains(record.DisplayName, trimmed)
            || Contains(record.Version, trimmed)
            || Contains(record.InternalName, trimmed);
    }

    static bool Contains(string value, string query)
    {
        return string.IsNullOrEmpty(value) == false
            && value.Contains(query, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// How many records survive, so a count and the list it opens cannot disagree.
    /// </summary>
    public static int Count(IEnumerable<DLLRecord>? records, string? query, bool allowDebugDlls)
    {
        if (records is null)
        {
            return 0;
        }

        var count = 0;

        foreach (var record in records)
        {
            if (Passes(record, query, allowDebugDlls))
            {
                ++count;
            }
        }

        return count;
    }
}
