using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>
/// A request to show only the games with one particular dll in place.
/// </summary>
/// <remarks>
/// The upscalers page can say that twelve games are using a file, and until now that was the end of
/// the sentence: there was no way to ask which twelve. This carries the question across to the games
/// page, and carries its own words with it, because the page it lands on has to be able to say what
/// it is showing and offer to stop.
/// </remarks>
public sealed class DllFilter
{
    public GameAssetType AssetType { get; }

    /// <summary>Matched the same way usage is counted: hash first, file version as a fallback.</summary>
    public string MD5Hash { get; }

    public string Version { get; }

    /// <summary>Reads as "Showing only games using DLSS v310.7".</summary>
    public string Label { get; }

    public DllFilter(GameAssetType assetType, string md5Hash, string version, string label)
    {
        AssetType = assetType;
        MD5Hash = md5Hash;
        Version = version;
        Label = label;
    }

    /// <summary>
    /// The sentence the games page shows while this filter is on.
    /// </summary>
    /// <remarks>
    /// Separate from building the filter so the wording can be tested without a dll record, which
    /// cannot be made outside the app. Both names are needed: "v310.7" alone does not say which
    /// upscaler, and three of them can carry the same version number.
    /// </remarks>
    public static string LabelFor(string assetTypeName, string versionName)
    {
        return ResourceHelper.GetFormattedResourceTemplate(
            "GamesPage_DllFilterTemplate", assetTypeName, versionName);
    }

    /// <summary>Whether a game currently has this dll in place.</summary>
    public bool Matches(Game game) => DllUsage.IsUsedBy(AssetType, MD5Hash, Version, game);
}
