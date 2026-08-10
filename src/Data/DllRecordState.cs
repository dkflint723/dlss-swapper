using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>
/// What a dll version's row says about where the file is.
/// </summary>
/// <remarks>
/// The page showed this only by which buttons a row had: a download button meant it was missing, a
/// delete button meant it was here. That is state carried by iconography alone, and it needs
/// knowing the icons already to read at all. A word says it outright.
/// </remarks>
public static class DllRecordState
{
    /// <summary>Reads as "On disk", "Imported" or "Not downloaded".</summary>
    public static string Describe(bool isDownloaded, bool isImported)
    {
        if (isImported)
        {
            // Checked first: an imported dll is on disk too, and where it came from is the more
            // useful of the two facts, because it is the one the manifest cannot vouch for.
            return ResourceHelper.GetString("Upscalers_Imported");
        }

        return isDownloaded
            ? ResourceHelper.GetString("Upscalers_OnDisk")
            : ResourceHelper.GetString("Upscalers_NotDownloaded");
    }

    /// <summary>
    /// The glyph beside the words, or empty when there is nothing to mark.
    /// </summary>
    /// <remarks>
    /// Not downloaded gets no glyph. It is the default state of most rows on this page, and marking
    /// every one of them would turn the absence of a mark into the exceptional case.
    /// </remarks>
    public static string Glyph(bool isDownloaded, bool isImported)
    {
        if (isImported)
        {
            return "\uE7B8";
        }

        return isDownloaded ? "\uE73E" : string.Empty;
    }
}
