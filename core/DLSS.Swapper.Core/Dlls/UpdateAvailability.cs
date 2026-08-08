using System.Collections.Generic;
using DLSS_Swapper.Data;

namespace DLSS_Swapper.Dlls;

/// <summary>
/// An installed dll reduced to what deciding "is this out of date" needs.
/// </summary>
/// <param name="AssetType">Which dll type it is.</param>
/// <param name="Rank">Its version, ranked by <see cref="Versioning.DllVersionRanking"/>.</param>
public readonly record struct InstalledDll(GameAssetType AssetType, ulong Rank);

/// <summary>
/// Works out which of a game's dll types have a newer version available.
/// </summary>
public static class UpdateAvailability
{
    /// <summary>
    /// The asset types where at least one installed dll is behind the newest available.
    /// </summary>
    /// <param name="installedDlls">
    /// Every installed dll, including repeats of the same type. A game can keep the same dll in
    /// several places at different versions and a swap updates all of them, so the type counts as
    /// out of date if any single location is behind.
    /// </param>
    /// <param name="latestRankByAssetType">
    /// The newest available version per type. Types absent from this are skipped rather than
    /// treated as up to date, because not knowing of a newer version is not the same as there
    /// being none.
    /// </param>
    /// <returns>Types in registry order, so the result does not depend on how the inputs were built.</returns>
    public static IReadOnlyList<GameAssetType> FindOutdatedTypes(
        IEnumerable<InstalledDll> installedDlls,
        IReadOnlyDictionary<GameAssetType, ulong> latestRankByAssetType)
    {
        var installedByType = new Dictionary<GameAssetType, List<ulong>>();
        foreach (var installedDll in installedDlls)
        {
            if (installedByType.TryGetValue(installedDll.AssetType, out var ranks) == false)
            {
                ranks = new List<ulong>();
                installedByType[installedDll.AssetType] = ranks;
            }

            ranks.Add(installedDll.Rank);
        }

        var outdatedAssetTypes = new List<GameAssetType>();

        foreach (var dllTypeDefinition in DllTypes.All)
        {
            if (latestRankByAssetType.TryGetValue(dllTypeDefinition.AssetType, out var latestRank) == false)
            {
                continue;
            }

            if (installedByType.TryGetValue(dllTypeDefinition.AssetType, out var installedRanks) == false)
            {
                continue;
            }

            foreach (var installedRank in installedRanks)
            {
                if (latestRank > installedRank)
                {
                    outdatedAssetTypes.Add(dllTypeDefinition.AssetType);
                    break;
                }
            }
        }

        return outdatedAssetTypes;
    }
}
