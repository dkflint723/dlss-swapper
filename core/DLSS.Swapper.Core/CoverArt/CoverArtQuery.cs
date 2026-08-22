using System.Text;

namespace DLSS_Swapper.CoverArt;

/// <summary>
/// What to ask SteamGridDB for, and what to leave alone.
/// </summary>
public static class CoverArtQuery
{
    /// <summary>
    /// The only sizes worth asking for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is one place in this app to put art: the 400x600 portrait a game's cover is drawn at.
    /// SteamGridDB also serves 460x215 and 920x430 horizontal capsules, and separately heroes,
    /// logos and icons. None of those have anywhere to go here, so asking for them would be bytes
    /// spent fetching images that cannot be shown.
    /// </para>
    /// <para>
    /// 600x900 is the exact 2:3 the slot is and is listed first for that reason. The other two are
    /// within a few percent of it - close enough that the resize, which preserves aspect and never
    /// crops, leaves them looking right - and they are what an obscure game tends to have when
    /// 600x900 has nothing at all.
    /// </para>
    /// </remarks>
    public const string PortraitDimensions = "600x900,660x930,342x482";

    /// <summary>
    /// The query for a game's portrait art.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Static only, and not as a preference. SteamGridDB's animated grids are webp and apng - it
    /// serves no animated gifs at all - and the covers here are drawn by a <c>BitmapImage</c>,
    /// which animates gif and nothing else. An animated grid would arrive, be flattened to one
    /// frame by the resize, and be shown as a still. Offering the choice would have meant labelling
    /// two options that produce the same result.
    /// </para>
    /// <para>
    /// <c>nsfw=false</c> is fixed, with no way to turn it off. Note that the parameter's third
    /// value, <c>true</c>, returns *only* flagged art rather than merely permitting it, so a
    /// well-meaning edit here is one word away from making this the opposite of what it says.
    /// <see cref="CoverArtJson.ReadImages"/> drops flagged art again on the way in, so losing this
    /// parameter cannot on its own put any in front of someone.
    /// </para>
    /// </remarks>
    public static string PortraitQuery()
    {
        return $"dimensions={PortraitDimensions}&types=static&nsfw=false";
    }

    /// <summary>
    /// The term to search a game's title with.
    /// </summary>
    /// <remarks>
    /// Store titles carry decoration the search index does not: trademark and copyright marks, and
    /// the runs of whitespace left behind once they are gone. Everything else is left alone,
    /// deliberately - stripping subtitles or edition suffixes would turn one accurate result into
    /// a page of near misses, and the user is picking from the list by name either way.
    /// </remarks>
    public static string SearchTermFor(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(title.Length);
        var lastWasSpace = false;

        foreach (var character in title)
        {
            if (character is '™' or '®' or '©')
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                // Collapsed rather than kept, because removing a mark from "Halo™ Infinite" leaves
                // two spaces behind and the index does not match through them.
                if (lastWasSpace == false)
                {
                    _ = builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            _ = builder.Append(character);
            lastWasSpace = false;
        }

        return builder.ToString().Trim();
    }
}
