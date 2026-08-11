using System.Collections.Generic;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.Data;

/// <summary>
/// How many games have a given dll in place right now.
/// </summary>
/// <remarks>
/// The one question the dll list could never answer. Everything else on that page describes the
/// file; this describes what would break if it went, which is the only thing anyone needs before
/// deleting one.
/// </remarks>
public static class DllUsage
{
    /// <summary>
    /// Whether a game currently has this exact dll installed.
    /// </summary>
    /// <remarks>
    /// Hash first and version only as a fallback, matching how an installed dll is resolved to a
    /// known version everywhere else: a hash is exact, but a dll a game shipped with is often not
    /// in the manifest at all and only its file version can be compared.
    /// </remarks>
    public static bool IsUsedBy(GameAssetType assetType, string md5Hash, string version, Game game)
    {
        foreach (var gameAsset in game.GameAssets)
        {
            if (gameAsset.AssetType != assetType)
            {
                continue;
            }

            if (string.IsNullOrEmpty(md5Hash) == false && string.IsNullOrEmpty(gameAsset.Hash) == false)
            {
                if (string.Equals(gameAsset.Hash, md5Hash, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (string.IsNullOrEmpty(version) == false && gameAsset.Version == version)
            {
                return true;
            }
        }

        return false;
    }

    public static int CountGamesUsing(GameAssetType assetType, string md5Hash, string version, IEnumerable<Game> games)
    {
        var count = 0;

        foreach (var game in games)
        {
            if (IsUsedBy(assetType, md5Hash, version, game))
            {
                ++count;
            }
        }

        return count;
    }

    /// <summary>
    /// Whether any game has this dll in place, which is what decides whether the count is worth
    /// offering to open.
    /// </summary>
    public static bool IsUsedByAny(GameAssetType assetType, string md5Hash, string version)
    {
        return CountGamesUsing(assetType, md5Hash, version, GameManager.Instance.GetSynchronisedGamesListCopy()) > 0;
    }

    /// <summary>
    /// The same answer, shaped for the row: what is shown when the file is in use, and what is
    /// shown when it is not.
    /// </summary>
    /// <remarks>
    /// Two functions rather than one plus a converter, because an <c>x:Bind</c> to a function
    /// ignores <c>Converter</c> entirely and fails the build rather than at runtime. Both sit here
    /// beside the rule they ask, so neither can drift from the count the row is showing.
    /// </remarks>
    public static Visibility UsedVisibility(GameAssetType assetType, string md5Hash, string version)
    {
        return IsUsedByAny(assetType, md5Hash, version) ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility NotUsedVisibility(GameAssetType assetType, string md5Hash, string version)
    {
        return IsUsedByAny(assetType, md5Hash, version) ? Visibility.Collapsed : Visibility.Visible;
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
