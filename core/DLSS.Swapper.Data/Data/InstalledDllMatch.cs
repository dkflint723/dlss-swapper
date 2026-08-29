namespace DLSS_Swapper.Data;

/// <summary>
/// Whether a game has a particular dll in place.
/// </summary>
/// <remarks>
/// Its own type rather than a member of DllUsage, because DllUsage answers questions about the
/// whole library for the library page and turns those answers into Visibility - all of which needs
/// a UI. This one question is asked by DllFilter, which is compiled without one.
/// </remarks>
internal static class InstalledDllMatch
{
    /// <summary>
    /// Whether a game currently has this exact dll installed.
    /// </summary>
    /// <remarks>
    /// Hash first and version only as a fallback, matching how an installed dll is resolved to a
    /// known version everywhere else: a hash is exact, but a dll a game shipped with is often not
    /// in the manifest at all and only its file version can be compared.
    /// </remarks>
    internal static bool IsUsedBy(GameAssetType assetType, string md5Hash, string version, Game game)
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
}
