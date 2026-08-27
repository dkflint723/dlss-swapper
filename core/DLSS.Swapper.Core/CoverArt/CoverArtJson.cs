using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace DLSS_Swapper.CoverArt;

/// <summary>
/// Reads SteamGridDB's two responses.
/// </summary>
/// <remarks>
/// <see cref="JsonDocument"/> rather than a serializer context, because none of these types outlive
/// the dialog that shows them and there is no reason for the app's source generated context to
/// learn about them. Kept here rather than beside the http client so the shapes can be tested
/// against captured responses without a network.
/// </remarks>
public static class CoverArtJson
{
    /// <summary>
    /// What SteamGridDB said went wrong, or null when the call succeeded.
    /// </summary>
    /// <remarks>
    /// Worth surfacing verbatim rather than replacing with our own wording: the two a user will
    /// actually hit are "Invalid key format" and an expired key, and both are about the thing they
    /// pasted into settings. A generic "could not load" would leave them nowhere.
    /// </remarks>
    public static string? ReadError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.TryGetProperty("success", out var success) &&
                success.ValueKind == JsonValueKind.False)
            {
                if (document.RootElement.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Array)
                {
                    var messages = new List<string>();

                    foreach (var error in errors.EnumerateArray())
                    {
                        if (error.ValueKind == JsonValueKind.String)
                        {
                            messages.Add(error.GetString() ?? string.Empty);
                        }
                    }

                    if (messages.Count > 0)
                    {
                        return string.Join(", ", messages);
                    }
                }

                return "Unknown error.";
            }

            return null;
        }
        catch (JsonException)
        {
            return "The response could not be read.";
        }
    }

    /// <summary>The games a search matched, in the order SteamGridDB ranked them.</summary>
    public static IReadOnlyList<CoverArtGame> ReadGames(string json)
    {
        var games = new List<CoverArtGame>();

        foreach (var element in EnumerateData(json))
        {
            if (TryReadInt(element, "id", out var id) == false)
            {
                continue;
            }

            var name = TryReadString(element, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                // Nothing to show in a list that is picked from by name.
                continue;
            }

            games.Add(new CoverArtGame()
            {
                Id = id,
                Name = name,
                Verified = element.TryGetProperty("verified", out var verified) && verified.ValueKind == JsonValueKind.True,
                Stores = ReadStores(element),
                ReleaseYear = ReadReleaseYear(element),
            });
        }

        return games;
    }

    /// <summary>
    /// The portrait art for a game, with anything not worth offering left out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// nsfw art is dropped here as well as being excluded by the request, so that a dropped query
    /// parameter cannot on its own put any in front of someone. There is no way to turn either off.
    /// Epilepsy-flagged art is dropped too, and can only be dropped here - it is the one flag the
    /// api gives no request parameter for.
    /// </para>
    /// <para>
    /// Joke art is deliberately *not* filtered. Nobody asked for it to be hidden and it is a fine
    /// thing to choose when you can see what you are choosing.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<CoverArtImage> ReadImages(string json)
    {
        var images = new List<CoverArtImage>();

        foreach (var element in EnumerateData(json))
        {
            if (IsFlagged(element, "nsfw") || IsFlagged(element, "epilepsy"))
            {
                continue;
            }

            if (TryReadInt(element, "id", out var id) == false)
            {
                continue;
            }

            var url = TryReadString(element, "url");
            var thumbnail = TryReadString(element, "thumb");

            if (IsFetchableUrl(url) == false || IsFetchableUrl(thumbnail) == false)
            {
                // A row with nothing to show and nothing to apply is not worth a tile. Dropped here
                // rather than left for the picker, which turns the thumbnail into a Uri without
                // asking and threw the whole page of results away on the one bad row.
                continue;
            }

            _ = TryReadInt(element, "width", out var width);
            _ = TryReadInt(element, "height", out var height);

            images.Add(new CoverArtImage()
            {
                Id = id,
                Url = url,
                ThumbnailUrl = thumbnail,
                Width = width,
                Height = height,
                Style = TryReadString(element, "style") ?? string.Empty,
                Author = ReadAuthorName(element),
            });
        }

        return images;
    }

    /// <summary>
    /// Whether this is a url we would be willing to fetch.
    /// </summary>
    /// <remarks>
    /// Absolute, and http or https. Anything else is not something to hand to an image control or a
    /// download - a relative path, a file:// or a data: url is at best broken and at worst a
    /// response telling this app to read something local.
    /// </remarks>
    static bool IsFetchableUrl([NotNullWhen(true)] string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
    }

    static IEnumerable<JsonElement> EnumerateData(string json)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
            if (document.RootElement.TryGetProperty("data", out var data) == false ||
                data.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var element in data.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    yield return element;
                }
            }
        }
    }

    static IReadOnlyList<string> ReadStores(JsonElement element)
    {
        if (element.TryGetProperty("types", out var types) == false || types.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var stores = new List<string>();

        foreach (var type in types.EnumerateArray())
        {
            if (type.ValueKind == JsonValueKind.String)
            {
                var value = type.GetString();
                if (string.IsNullOrWhiteSpace(value) == false)
                {
                    stores.Add(value);
                }
            }
        }

        return stores;
    }

    /// <summary>
    /// The release year, from a unix timestamp.
    /// </summary>
    /// <remarks>
    /// Only the year is kept. It is there to tell two games with the same name apart, and a full
    /// date in a list that is already carrying a name and a row of stores is noise.
    /// </remarks>
    static int? ReadReleaseYear(JsonElement element)
    {
        if (element.TryGetProperty("release_date", out var releaseDate) == false ||
            releaseDate.ValueKind != JsonValueKind.Number ||
            releaseDate.TryGetInt64(out var seconds) == false)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).Year;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>The uploader's name, out of the author object the api nests it in.</summary>
    static string ReadAuthorName(JsonElement element)
    {
        if (element.TryGetProperty("author", out var author) == false || author.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        return TryReadString(author, "name") ?? string.Empty;
    }

    static bool IsFlagged(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
    }

    static bool TryReadInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;

        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    static string? TryReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
