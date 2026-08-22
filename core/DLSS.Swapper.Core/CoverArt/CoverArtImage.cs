namespace DLSS_Swapper.CoverArt;

/// <summary>
/// One piece of portrait art, as offered to the user before anything is written.
/// </summary>
public sealed class CoverArtImage
{
    public required int Id { get; init; }

    /// <summary>The full image, fetched only once this one is chosen.</summary>
    public required string Url { get; init; }

    /// <summary>The small copy, which is what the picker shows a page of.</summary>
    public required string ThumbnailUrl { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    /// <summary>SteamGridDB's own grouping: alternate, blurred, white_logo, material, no_logo.</summary>
    public required string Style { get; init; }

    /// <summary>Who uploaded it. Shown because the art is someone's work and this is the only credit it gets.</summary>
    public required string Author { get; init; }
}
