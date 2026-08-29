using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using DLSS_Swapper.Dlls;

namespace DLSS_Swapper.Data;

/// <summary>
/// The list of dlls available to swap to, plus the hashes of dlls games are known to ship with.
/// </summary>
/// <remarks>
/// Where the nine record lists used to be named properties, they are now keyed by asset type, so a
/// new upscaler needs only a row in <see cref="DllTypes"/>. The json is unchanged.
/// </remarks>
[JsonConverter(typeof(ManifestConverter))]
internal class Manifest
{
    public DllKeyedRecords<DLLRecord> Records { get; set; } = new DllKeyedRecords<DLLRecord>();

    public DllKeyedRecords<HashedKnownDLL> KnownDLLs { get; set; } = new DllKeyedRecords<HashedKnownDLL>();

    /// <summary>The records this manifest carries for an asset type.</summary>
    public List<DLLRecord>? GetRecords(GameAssetType assetType) => Records.GetRecords(assetType);
}

/// <summary>
/// Reads and writes the manifest's flat json shape.
/// </summary>
/// <remarks>
/// The record keys and "known_dlls" sit side by side at the top level, so this cannot simply defer
/// to the keyed records converter. It delegates each property to the same tested core helpers
/// instead of repeating their logic, because this writes the user's imported manifest back to disk.
/// </remarks>
internal sealed class ManifestConverter : JsonConverter<Manifest>
{
    const string KnownDllsPropertyName = "known_dlls";

    public override Manifest Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected an object for the manifest but found {reader.TokenType}.");
        }

        var manifest = new Manifest();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return manifest;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected a property name but found {reader.TokenType}.");
            }

            var propertyName = reader.GetString() ?? string.Empty;
            reader.Read();

            if (propertyName == KnownDllsPropertyName)
            {
                if (reader.TokenType != JsonTokenType.Null)
                {
                    manifest.KnownDLLs = ReadKnownDlls(ref reader, options);
                }

                continue;
            }

            DllKeyedRecordsJson.ReadProperty(propertyName, ref reader, options, manifest.Records);
        }

        throw new JsonException("Unexpected end of json while reading the manifest.");
    }

    static DllKeyedRecords<HashedKnownDLL> ReadKnownDlls(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected an object for {KnownDllsPropertyName} but found {reader.TokenType}.");
        }

        var knownDLLs = new DllKeyedRecords<HashedKnownDLL>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return knownDLLs;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected a property name but found {reader.TokenType}.");
            }

            var propertyName = reader.GetString() ?? string.Empty;
            reader.Read();

            DllKeyedRecordsJson.ReadProperty(propertyName, ref reader, options, knownDLLs);
        }

        throw new JsonException($"Unexpected end of json while reading {KnownDllsPropertyName}.");
    }

    public override void Write(Utf8JsonWriter writer, Manifest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        DllKeyedRecordsJson.WriteProperties(writer, value.Records, options);

        writer.WritePropertyName(KnownDllsPropertyName);
        writer.WriteStartObject();
        DllKeyedRecordsJson.WriteProperties(writer, value.KnownDLLs, options);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}
