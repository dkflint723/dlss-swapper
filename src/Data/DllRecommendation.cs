using System.Text.Json.Serialization;

namespace DLSS_Swapper.Data;

/// <summary>
/// One curated claim about one exact dll build, and the sentence behind it.
/// </summary>
/// <remarks>
/// <para>
/// The upscalers list is a wall of a hundred version numbers with no way to tell which two
/// matter. The knowledge — that the 310.x line is the transformer model, that 3.8.10 is the last
/// CNN build worth keeping — lives on forums, which is to say nowhere this app's user is when
/// choosing. These entries carry it into the list, each with its why said outright.
/// </para>
/// <para>
/// An entry names an exact version, never a line: a recommendation is a considered claim about
/// one build, and it must not transfer by pattern-match to whatever ships next under a similar
/// number. A version that leaves the manifest simply stops matching. The file rides the app as an
/// embedded resource beside the static manifest, so updating a claim is a reviewed change like
/// any other.
/// </para>
/// </remarks>
public class DllRecommendation
{
    /// <summary>The <see cref="GameAssetType"/> name, eg. "DLSS".</summary>
    [JsonPropertyName("asset_type")]
    public string AssetType { get; set; } = string.Empty;

    /// <summary>The manifest's exact version string, eg. "310.7.129.0".</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("why")]
    public string Why { get; set; } = string.Empty;
}
