using System.Collections.Generic;

namespace DLSS_Swapper.CoverArt;

/// <summary>
/// Turns SteamGridDB's store keys into the names those stores are actually called.
/// </summary>
/// <remarks>
/// These sit beside a game's name in the list a user picks from, next to a release year, and they
/// are there to tell two identically named games apart. "egs" does not do that job for anybody who
/// has not read the api docs; "Epic" does.
/// </remarks>
public static class CoverArtStores
{
    static readonly Dictionary<string, string> _displayNames = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["steam"] = "Steam",
        ["gog"] = "GOG",
        ["egs"] = "Epic",
        ["origin"] = "EA",
        ["uplay"] = "Ubisoft",
        ["flashpoint"] = "Flashpoint",
    };

    /// <summary>
    /// The store's name, or the key itself when it is one we have not seen.
    /// </summary>
    /// <remarks>
    /// An unknown key is passed through rather than dropped. SteamGridDB can add a store without
    /// telling anyone, and a row reading "Doom · 2016" with the store silently missing is worse
    /// than one reading "Doom · 2016 · itch" - the whole point of the line is telling rows apart.
    /// </remarks>
    public static string DisplayName(string? store)
    {
        if (string.IsNullOrWhiteSpace(store))
        {
            return string.Empty;
        }

        return _displayNames.TryGetValue(store, out var displayName) ? displayName : store;
    }
}
