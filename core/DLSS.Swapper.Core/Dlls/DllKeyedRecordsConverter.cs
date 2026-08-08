using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DLSS_Swapper.Dlls;

/// <summary>
/// Supplies a converter for any type deriving from <see cref="DllKeyedRecords{TRecord}"/>.
/// </summary>
public sealed class DllKeyedRecordsConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => FindKeyedRecordsBase(typeToConvert) is not null;

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var keyedRecordsBase = FindKeyedRecordsBase(typeToConvert);
        if (keyedRecordsBase is null)
        {
            return null;
        }

        var recordType = keyedRecordsBase.GetGenericArguments()[0];
        var converterType = typeof(DllKeyedRecordsConverter<,>).MakeGenericType(typeToConvert, recordType);

        return (JsonConverter?)Activator.CreateInstance(converterType);
    }

    /// <summary>Walks up to the DllKeyedRecords&lt;T&gt; a type derives from, if any.</summary>
    static Type? FindKeyedRecordsBase(Type type)
    {
        for (var current = (Type?)type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(DllKeyedRecords<>))
            {
                return current;
            }
        }

        return null;
    }
}

/// <summary>
/// The property level reading and writing behind <see cref="DllKeyedRecords{TRecord}"/>.
/// </summary>
/// <remarks>
/// Exposed so a containing object, such as the manifest which also carries a known dll list, can
/// reuse this rather than repeating the loop. Repeating it would mean a second, untested copy of
/// logic that decides how a user's imported manifest is written back.
/// </remarks>
public static class DllKeyedRecordsJson
{
    /// <summary>
    /// Reads one property into <paramref name="target"/>, whether or not the registry knows the key.
    /// </summary>
    /// <remarks>The reader must be positioned on the property's value.</remarks>
    public static void ReadProperty<TRecord>(string propertyName, ref Utf8JsonReader reader, JsonSerializerOptions options, DllKeyedRecords<TRecord> target)
    {
        var dllTypeDefinition = DllTypes.ForManifestKey(propertyName);
        if (dllTypeDefinition is null)
        {
            // Kept verbatim. A manifest written by a newer release can carry types we have never
            // heard of, and dropping them here would quietly corrupt it on the next save.
            target.UnknownKeys[propertyName] = JsonElement.ParseValue(ref reader).GetRawText();
            return;
        }

        target.SetRecords(dllTypeDefinition.AssetType, ReadRecords<TRecord>(ref reader, options));
    }

    static List<TRecord> ReadRecords<TRecord>(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var records = new List<TRecord>();

        if (reader.TokenType == JsonTokenType.Null)
        {
            return records;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected an array of records but found {reader.TokenType}.");
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            // One record at a time, so only the record type needs to be known to the serializer.
            // That keeps this working with a source generated context without every List<T> also
            // having to be registered there.
            var record = JsonSerializer.Deserialize<TRecord>(ref reader, options);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    /// <summary>
    /// Writes every record key, then anything we did not recognise, without opening an object.
    /// </summary>
    public static void WriteProperties<TRecord>(Utf8JsonWriter writer, DllKeyedRecords<TRecord> records, JsonSerializerOptions options)
    {
        // Registry order, so the output is stable rather than dictionary dependent.
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            var typeRecords = records.GetRecords(dllTypeDefinition.AssetType);
            if (typeRecords is null)
            {
                continue;
            }

            writer.WritePropertyName(dllTypeDefinition.ManifestKey);
            writer.WriteStartArray();

            foreach (var record in typeRecords)
            {
                JsonSerializer.Serialize(writer, record, options);
            }

            writer.WriteEndArray();
        }

        foreach (var unknownKey in records.UnknownKeys)
        {
            writer.WritePropertyName(unknownKey.Key);
            writer.WriteRawValue(unknownKey.Value);
        }
    }
}

/// <summary>
/// Reads and writes a json object of manifest key to record list.
/// </summary>
/// <remarks>
/// Records are handled one at a time rather than as a list, so only the record type itself needs to
/// be known to the serializer. That keeps this working with the app's source generated context
/// without every List&lt;T&gt; also having to be registered there.
/// </remarks>
internal sealed class DllKeyedRecordsConverter<TSelf, TRecord> : JsonConverter<TSelf>
    where TSelf : DllKeyedRecords<TRecord>, new()
{
    public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected an object for {typeToConvert.Name} but found {reader.TokenType}.");
        }

        var result = new TSelf();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return result;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected a property name but found {reader.TokenType}.");
            }

            var propertyName = reader.GetString() ?? string.Empty;
            reader.Read();

            DllKeyedRecordsJson.ReadProperty(propertyName, ref reader, options, result);
        }

        throw new JsonException("Unexpected end of json while reading records.");
    }

    public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        DllKeyedRecordsJson.WriteProperties(writer, value, options);
        writer.WriteEndObject();
    }
}
