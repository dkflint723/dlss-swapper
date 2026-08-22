using System.Collections.Generic;

namespace DLSS_Swapper.CoverArt;

/// <summary>
/// One game the search matched, as offered to the user to pick from.
/// </summary>
/// <remarks>
/// The name on its own is not enough to choose by. A search for "doom" returns two games called
/// exactly "Doom", one from 1993 and one from 2016, so the year and the stores a game is on are
/// carried here to be shown beside it - they are the only things that tell those two apart.
/// </remarks>
public sealed class CoverArtGame
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Whether SteamGridDB has checked this entry, shown so a user can prefer one that is.</summary>
    public required bool Verified { get; init; }

    /// <summary>The stores it is sold on, as SteamGridDB names them: steam, gog, egs, origin, uplay, flashpoint.</summary>
    public required IReadOnlyList<string> Stores { get; init; }

    /// <summary>Release year, or null when the entry has no release date.</summary>
    public required int? ReleaseYear { get; init; }
}
