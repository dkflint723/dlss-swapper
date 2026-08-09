using System;
using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Empties DLLManager's records for the duration of a test and puts them back afterwards, so a
/// test states exactly which dll versions exist rather than depending on whatever the app last
/// loaded.
/// </summary>
internal sealed class ManifestScope : IDisposable
{
    readonly Dictionary<GameAssetType, List<DLLRecord>> _saved = new Dictionary<GameAssetType, List<DLLRecord>>();

    internal ManifestScope()
    {
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            var records = DLLManager.Instance.GetRecords(dllTypeDefinition.AssetType);
            if (records is null)
            {
                continue;
            }

            _saved[dllTypeDefinition.AssetType] = records.ToList();
            records.Clear();
        }
    }

    /// <summary>Declares that a version of a dll exists to swap to.</summary>
    /// <param name="internalName">
    /// The sdk version, which only the types ranked by it use. Defaults to the file version.
    /// </param>
    internal DLLRecord Add(GameAssetType assetType, string version, string? internalName = null, string md5Hash = "")
    {
        var dllRecord = new DLLRecord()
        {
            AssetType = assetType,
            Version = version,
            InternalName = internalName ?? version,
            MD5Hash = md5Hash,
        };

        DLLManager.Instance.GetRecords(assetType)!.Add(dllRecord);
        return dllRecord;
    }

    public void Dispose()
    {
        foreach (var savedRecords in _saved)
        {
            var records = DLLManager.Instance.GetRecords(savedRecords.Key);
            if (records is null)
            {
                continue;
            }

            records.Clear();
            foreach (var dllRecord in savedRecords.Value)
            {
                records.Add(dllRecord);
            }
        }
    }
}

/// <summary>
/// Tests that swap DLLManager's records share one process wide singleton, so they must not run at
/// the same time as each other.
/// </summary>
[CollectionDefinition(Name)]
public class ManifestCollection
{
    public const string Name = "manifest";
}
