using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the curated lane: the file's own integrity, and how tagged records group.
/// </summary>
/// <remarks>
/// A recommendation is the one place this app editorialises, so the claims have to stay
/// checkable: every entry must name a real type and a build the shipped manifest actually
/// carries. A note whose version has drifted out of the manifest is a claim about nothing,
/// silently shown to nobody — these tests make that a build failure instead.
/// </remarks>
public class DllRecommendationTests
{
    static List<DllRecommendation> LoadRecommendations()
    {
        var assembly = typeof(DLLRecord).Assembly;
        using var stream = assembly.GetManifestResourceStream("DLSS_Swapper.Assets.recommended.json");
        Assert.NotNull(stream);

        var recommendations = JsonSerializer.Deserialize<List<DllRecommendation>>(stream);
        Assert.NotNull(recommendations);
        return recommendations;
    }

    [Fact]
    public void EveryRecommendationNamesARealTypeAndCarriesItsWhy()
    {
        var recommendations = LoadRecommendations();

        Assert.NotEmpty(recommendations);

        foreach (var recommendation in recommendations)
        {
            Assert.True(System.Enum.TryParse<GameAssetType>(recommendation.AssetType, out _),
                $"'{recommendation.AssetType}' is not a GameAssetType.");
            Assert.False(string.IsNullOrWhiteSpace(recommendation.Version));
            Assert.False(string.IsNullOrWhiteSpace(recommendation.Why),
                "A recommendation without its why is a bare flag, which is the thing this lane exists not to be.");
        }

        // One claim per build: two entries for the same file would race for the same note.
        Assert.Equal(recommendations.Count, recommendations.Select(x => (x.AssetType, x.Version)).Distinct().Count());
    }

    [Fact]
    public void EveryRecommendedVersionExistsInTheShippedManifest()
    {
        var assembly = typeof(DLLRecord).Assembly;
        using var manifestStream = assembly.GetManifestResourceStream("DLSS_Swapper.Assets.static_manifest.json");
        Assert.NotNull(manifestStream);

        using var manifestJson = JsonDocument.Parse(manifestStream);

        foreach (var recommendation in LoadRecommendations())
        {
            // The manifest keys are the lowercased type names, eg. "dlss", "dlss_g".
            var key = recommendation.AssetType.ToLowerInvariant();
            Assert.True(manifestJson.RootElement.TryGetProperty(key, out var records),
                $"The manifest has no '{key}' list.");

            var versions = records.EnumerateArray()
                .Select(x => x.GetProperty("version").GetString())
                .ToList();

            Assert.Contains(recommendation.Version, versions);
        }
    }

    [Fact]
    public void RecommendedVersionsLeadTheListInTheirOwnGroup()
    {
        var records = new List<DLLRecord>()
        {
            new DLLRecord() { Version = "310.7.129.0", RecommendationNote = "the current line" },
            new DLLRecord() { Version = "310.7.128.0" },
            new DLLRecord() { Version = "3.8.10.0", RecommendationNote = "the last CNN build" },
            new DLLRecord() { Version = "3.8.0.0" },
        };

        var groups = DllVersionGroup.Build(records, "DLSS");

        Assert.Equal(ResourceHelper.GetString("DllGroup_Recommended"), groups[0].Label);
        Assert.Equal(new[] { "310.7.129.0", "3.8.10.0" }, groups[0].Versions.Select(x => x.Version));

        // Moved, not duplicated: one record in two groups is one selection highlighted twice.
        var rest = groups.Skip(1).SelectMany(x => x.Versions).ToList();
        Assert.Equal(new[] { "310.7.128.0", "3.8.0.0" }, rest.Select(x => x.Version));
    }

    [Fact]
    public void AListWithNothingRecommendedGroupsExactlyAsBefore()
    {
        var records = new List<DLLRecord>()
        {
            new DLLRecord() { Version = "2.0.2.68" },
            new DLLRecord() { Version = "2.0.1.41" },
        };

        var groups = DllVersionGroup.Build(records, "XeSS");

        Assert.All(groups, x => Assert.NotEqual(ResourceHelper.GetString("DllGroup_Recommended"), x.Label));
        Assert.Equal(2, groups.SelectMany(x => x.Versions).Count());
    }
}
