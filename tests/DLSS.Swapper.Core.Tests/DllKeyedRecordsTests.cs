using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Xunit;

namespace DLSS_Swapper.Tests;

public class DllKeyedRecordsTests
{
    /// <summary>Stand-in for the app's record types, which cannot be reached from here.</summary>
    public class TestRecord
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    static DllKeyedRecords<TestRecord> Read(string json) => JsonSerializer.Deserialize<DllKeyedRecords<TestRecord>>(json)!;

    static string Write(DllKeyedRecords<TestRecord> records) => JsonSerializer.Serialize(records);

    #region The wire format has to stay exactly as it was

    [Fact]
    public void Read_MapsEachManifestKeyOntoItsAssetType()
    {
        var records = Read("""
            {"dlss":[{"version":"310.7.0.0"}],"xess":[{"version":"2.0.1.0"},{"version":"2.0.2.0"}]}
            """);

        Assert.Equal("310.7.0.0", Assert.Single(records.GetRecords(GameAssetType.DLSS)!).Version);
        Assert.Equal(2, records.GetRecords(GameAssetType.XeSS)!.Count);
    }

    [Fact]
    public void Read_LeavesAbsentKeysAsEmptyRatherThanNull()
    {
        var records = Read("""{"dlss":[{"version":"310.7.0.0"}]}""");

        Assert.All(DllTypes.All, x => Assert.NotNull(records.GetRecords(x.AssetType)));
        Assert.Empty(records.GetRecords(GameAssetType.XeLL)!);
    }

    [Fact]
    public void Write_UsesTheManifestKeysAndRegistryOrder()
    {
        var records = new DllKeyedRecords<TestRecord>();
        records.SetRecords(GameAssetType.DLSS, [new TestRecord() { Version = "1.0.0.0" }]);

        var json = Write(records);

        using var document = JsonDocument.Parse(json);
        var writtenKeys = document.RootElement.EnumerateObject().Select(x => x.Name).ToList();

        Assert.Equal(DllTypes.All.Select(x => x.ManifestKey), writtenKeys);
    }

    [Fact]
    public void RoundTrip_PreservesEveryRecord()
    {
        var original = new DllKeyedRecords<TestRecord>();
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            original.SetRecords(dllTypeDefinition.AssetType, [new TestRecord() { Version = dllTypeDefinition.ManifestKey }]);
        }

        var roundTripped = Read(Write(original));

        Assert.All(DllTypes.All, x =>
            Assert.Equal(x.ManifestKey, Assert.Single(roundTripped.GetRecords(x.AssetType)!).Version));
    }

    [Fact]
    public void RoundTrip_IsStableAcrossRepeatedSaves()
    {
        var json = """{"dlss":[{"version":"310.7.0.0"}],"xess":[{"version":"2.0.1.0"}]}""";

        var once = Write(Read(json));
        var twice = Write(Read(once));

        Assert.Equal(once, twice);
    }

    #endregion

    #region A key we do not recognise must survive

    /// <summary>
    /// A manifest written by a newer release can carry a dll type this build has never heard of.
    /// Dropping it would corrupt the user's imported manifest the next time it is saved.
    /// </summary>
    [Fact]
    public void RoundTrip_PreservesKeysTheRegistryDoesNotKnow()
    {
        var json = """{"dlss":[],"fsr_40_dx12":[{"version":"4.0.0.0"}]}""";

        var written = Write(Read(json));

        using var document = JsonDocument.Parse(written);
        Assert.True(document.RootElement.TryGetProperty("fsr_40_dx12", out var unknown));
        Assert.Equal("4.0.0.0", unknown[0].GetProperty("version").GetString());
    }

    [Fact]
    public void RoundTrip_PreservesUnknownKeysOfAnyShape()
    {
        var json = """{"dlss":[],"schema_version":3,"generated_by":"manifest-builder"}""";

        var written = Write(Read(json));

        using var document = JsonDocument.Parse(written);
        Assert.Equal(3, document.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("manifest-builder", document.RootElement.GetProperty("generated_by").GetString());
    }

    #endregion

    #region Malformed input

    [Fact]
    public void Read_RejectsANonObject()
    {
        Assert.Throws<JsonException>(() => Read("[]"));
    }

    [Fact]
    public void Read_RejectsARecordListThatIsNotAnArray()
    {
        Assert.Throws<JsonException>(() => Read("""{"dlss":{"version":"1.0.0.0"}}"""));
    }

    [Fact]
    public void Read_TreatsANullRecordListAsEmpty()
    {
        var records = Read("""{"dlss":null}""");

        Assert.Empty(records.GetRecords(GameAssetType.DLSS)!);
    }

    #endregion

    /// <summary>The known dll list is the first real use of this shape, so it gets a direct check.</summary>
    [Fact]
    public void KnownDlls_RoundTripThroughTheSameShape()
    {
        var json = """
            {"dlss":[{"hash":"ABC123","version":"310.7.0.0","sources":{"Steam":["dGl0bGU="]}}]}
            """;

        var knownDLLs = JsonSerializer.Deserialize<DllKeyedRecords<HashedKnownDLL>>(json)!;
        var hashes = knownDLLs.GetRecords(GameAssetType.DLSS)!;

        Assert.Equal("ABC123", Assert.Single(hashes).Hash);
        Assert.Equal(["dGl0bGU="], hashes[0].Sources["Steam"]);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(knownDLLs));
        Assert.Equal("ABC123", document.RootElement.GetProperty("dlss")[0].GetProperty("hash").GetString());
    }
}
