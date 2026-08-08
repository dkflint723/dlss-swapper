using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using DLSS_Swapper.Data;

namespace DLSS_Swapper.Dlls;

/// <summary>
/// A json object whose properties are dll manifest keys, each holding a list of records.
/// </summary>
/// <remarks>
/// <para>
/// Both the dll manifest and its known dll list have this shape. Representing it as a map keyed by
/// asset type means a new upscaler needs nothing here, only a row in <see cref="DllTypes"/>.
/// </para>
/// <para>
/// The json is unchanged from the nine named properties this replaced. Keys are written in registry
/// order, and any key the registry does not know is preserved untouched so that a manifest carrying
/// a type built by a newer release still round trips through an older one.
/// </para>
/// </remarks>
[JsonConverter(typeof(DllKeyedRecordsConverterFactory))]
public class DllKeyedRecords<TRecord>
{
    readonly Dictionary<GameAssetType, List<TRecord>> _records =
        DllTypes.All.ToDictionary(x => x.AssetType, x => new List<TRecord>());

    /// <summary>
    /// Properties we did not recognise, kept so writing back does not silently drop them.
    /// </summary>
    internal Dictionary<string, string> UnknownKeys { get; } = new Dictionary<string, string>();

    /// <summary>The records for an asset type, or null if it is not a swappable one.</summary>
    public List<TRecord>? GetRecords(GameAssetType assetType)
    {
        return _records.TryGetValue(assetType, out var records) ? records : null;
    }

    public void SetRecords(GameAssetType assetType, List<TRecord> records)
    {
        _records[assetType] = records;
    }

    public IReadOnlyDictionary<GameAssetType, List<TRecord>> All => _records;
}
