using System.Text.Json.Serialization;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Data;

public class GameLibrarySettings
{
    [JsonPropertyName("GameLibrary")]
    [JsonConverter(typeof(JsonStringEnumConverter<GameLibrary>))]
    public GameLibrary GameLibrary { get; set; }

    [JsonPropertyName("IsEnabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether this library's section on the games page is folded shut.</summary>
    /// <remarks>
    /// Here rather than in its own setting because it is one fact per library, and this is already
    /// the list of those. Nothing writes it through the array setter, so a toggle has to save the
    /// settings itself.
    /// </remarks>
    [JsonPropertyName("IsCollapsed")]
    public bool IsCollapsed { get; set; } = false;
}
