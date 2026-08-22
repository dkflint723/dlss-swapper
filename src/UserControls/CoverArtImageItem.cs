using System;
using System.Globalization;
using DLSS_Swapper.CoverArt;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// One cover on offer.
/// </summary>
/// <remarks>
/// The thumbnail is what the picker shows a page of; the full image is only fetched once one is
/// chosen. The author's name is carried because the art is someone's work and this is the only
/// credit it gets anywhere in the app.
/// </remarks>
public sealed class CoverArtImageItem
{
    public int Id { get; }

    public string Url { get; }

    public Uri ThumbnailUri { get; }

    /// <summary>Size and who made it, shown under the thumbnail.</summary>
    public string Detail { get; }

    public CoverArtImageItem(CoverArtImage image)
    {
        Id = image.Id;
        Url = image.Url;
        ThumbnailUri = new Uri(image.ThumbnailUrl);

        var size = string.Format(CultureInfo.CurrentCulture, "{0}×{1}", image.Width, image.Height);

        Detail = string.IsNullOrWhiteSpace(image.Author)
            ? size
            : $"{size} · {ResourceHelper.GetFormattedResourceTemplate("CoverArt_ByTemplate", image.Author)}";
    }
}
