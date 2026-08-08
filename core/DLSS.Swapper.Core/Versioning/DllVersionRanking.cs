using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;

namespace DLSS_Swapper.Versioning;

/// <summary>
/// Decides which of a dll's two version numbers to rank it by.
/// </summary>
/// <remarks>
/// <para>
/// Most dlls have one version. FSR has two: the SDK version it reports internally, such as 3.1.4,
/// and the file version of the dll itself, such as 1.0.1.41314. They do not move together. A later
/// 3.1.2 build ships as file version 1.0.2.38022, which is numerically above 3.1.4's file version,
/// so ranking FSR by file version puts the older SDK on top.
/// </para>
/// <para>
/// Picking the newest and deciding whether something is out of date must use the same answer. When
/// they disagreed, the newest was chosen by SDK version and then compared by file version, which
/// reported a game as up to date while a newer FSR was available.
/// </para>
/// </remarks>
public static class DllVersionRanking
{
    /// <summary>
    /// Ranks a dll, given both the version it reports internally and its file version.
    /// </summary>
    /// <param name="assetType">The type being ranked.</param>
    /// <param name="internalVersion">The SDK version, used only by types that report one.</param>
    /// <param name="fileVersion">The dll's own file version.</param>
    /// <returns>False when neither version could be parsed, in which case no comparison is safe.</returns>
    public static bool TryGetRank(GameAssetType assetType, string? internalVersion, string? fileVersion, out ulong rank)
    {
        if (DllTypes.ForAssetTypeIncludingBackup(assetType)?.VersionFromInternalName == true
            && DllVersion.TryParse(internalVersion, out rank))
        {
            return true;
        }

        // Either the type ranks by file version, or it ranks by SDK version but we could not read
        // one. Falling back keeps a dll comparable rather than silently dropping out of the ranking.
        return DllVersion.TryParse(fileVersion, out rank);
    }
}
