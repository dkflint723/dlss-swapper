using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.Data;

/// <summary>
/// The same answers, as something XAML can bind to.
/// </summary>
/// <remarks>
/// Whether a dll is in use is a fact about the library and is worked out in DllUsage itself, which
/// is compiled without any UI. Turning that fact into a Visibility is a presentation decision and
/// needs WinUI, so it lives here.
/// </remarks>
public static class DllUsageVisibility
{
    public static Visibility UsedVisibility(GameAssetType assetType, string md5Hash, string version)
    {
        return DllUsage.IsUsedByAny(assetType, md5Hash, version) ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility NotUsedVisibility(GameAssetType assetType, string md5Hash, string version)
    {
        return DllUsage.IsUsedByAny(assetType, md5Hash, version) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Reads as "14 games", "1 game", or "Not used".
    /// </summary>
    /// <remarks>
    /// "Not used" rather than "0 games", because zero is the answer that matters and a bare zero
    /// reads like a value that failed to load.
    /// </remarks>
    public static string DescribeUsage(GameAssetType assetType, string md5Hash, string version)
    {
        return DescribeCount(CountGamesUsing(assetType, md5Hash, version, GameManager.Instance.GetSynchronisedGamesListCopy()));
    }

    /// <summary>
    /// The words for a count.
    /// </summary>
    /// <remarks>
    /// Separate from the count itself because the library lives on a singleton that cannot be built
    /// outside the app, and the wording is the part worth pinning down.
    /// </remarks>
    public static string DescribeCount(int count)
    {
        if (count == 0)
        {
            return ResourceHelper.GetString("Upscalers_NotUsed");
        }

        return count == 1
            ? ResourceHelper.GetString("Upscalers_UsedByOneGame")
            : ResourceHelper.GetFormattedResourceTemplate("Upscalers_UsedByGamesTemplate", count);
    }
}
