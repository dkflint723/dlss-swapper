using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.CoverArt;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// One row in the list of games a search matched.
/// </summary>
/// <remarks>
/// The name alone cannot be picked from — searching "doom" returns two games called exactly "Doom",
/// one from 1993 and one from 2016. <see cref="Detail"/> is the line that tells them apart, and it
/// is why the search returns a list to choose from rather than guessing.
/// </remarks>
public sealed class CoverArtGameItem
{
    public int Id { get; }

    public string Name { get; }

    /// <summary>Release year, stores and whether the entry is verified, in that order.</summary>
    public string Detail { get; }

    public CoverArtGameItem(CoverArtGame game)
    {
        Id = game.Id;
        Name = game.Name;
        Detail = BuildDetail(game);
    }

    static string BuildDetail(CoverArtGame game)
    {
        var parts = new List<string>();

        if (game.ReleaseYear is int year)
        {
            parts.Add(year.ToString(System.Globalization.CultureInfo.CurrentCulture));
        }

        var stores = game.Stores.Select(CoverArtStores.DisplayName).Where(x => string.IsNullOrEmpty(x) == false).ToList();
        if (stores.Count > 0)
        {
            parts.Add(string.Join(", ", stores));
        }

        if (game.Verified)
        {
            parts.Add(ResourceHelper.GetString("CoverArt_Verified"));
        }

        return string.Join(" · ", parts);
    }
}
