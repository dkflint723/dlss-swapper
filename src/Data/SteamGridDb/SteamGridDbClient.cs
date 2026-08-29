using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.CoverArt;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data.SteamGridDb;

/// <summary>
/// Something SteamGridDB refused to do, carrying the wording it used.
/// </summary>
internal sealed class SteamGridDbException : Exception
{
    internal SteamGridDbException(string message) : base(message)
    {
    }
}

/// <summary>
/// Searches SteamGridDB for cover art.
/// </summary>
/// <remarks>
/// <para>
/// Only the two calls the picker makes, and only portraits. See <see cref="CoverArtQuery"/> for why
/// the horizontal capsules, heroes, logos and icons are never asked for: there is one 400x600 slot
/// in this app and nothing else has anywhere to go.
/// </para>
/// <para>
/// The key goes on each request rather than onto the shared client's default headers. That client
/// is the app's one <see cref="HttpClient"/> and it is used for dll downloads and the manifest too,
/// none of which should be sending someone's api key to beeradmoore.github.io.
/// </para>
/// </remarks>
internal static class SteamGridDbClient
{
    const string BaseUrl = "https://www.steamgriddb.com/api/v2";

    /// <summary>Whether a key has been set. The feature stays out of the way until one is.</summary>
    internal static bool HasApiKey => string.IsNullOrWhiteSpace(Settings.Instance.SteamGridDbApiKey) == false;

    /// <summary>The games matching a title, for the user to pick the right one out of.</summary>
    internal static async Task<IReadOnlyList<CoverArtGame>> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var searchTerm = CoverArtQuery.SearchTermFor(term);

        if (string.IsNullOrEmpty(searchTerm))
        {
            return Array.Empty<CoverArtGame>();
        }

        var body = await GetAsync($"{BaseUrl}/search/autocomplete/{Uri.EscapeDataString(searchTerm)}", cancellationToken).ConfigureAwait(false);

        return CoverArtJson.ReadGames(body);
    }

    /// <summary>
    /// The portrait art for one game.
    /// </summary>
    /// <remarks>
    /// What is asked for, and why, is <see cref="CoverArtQuery.PortraitQuery"/>'s to say - it is
    /// tested, and this is not. The commas in the dimensions list are left unescaped deliberately:
    /// they are legal in a query value and SteamGridDB matches on them literally.
    /// </remarks>
    internal static async Task<IReadOnlyList<CoverArtImage>> GetPortraitsAsync(int gameId, CancellationToken cancellationToken = default)
    {
        var body = await GetAsync($"{BaseUrl}/grids/game/{gameId}?{CoverArtQuery.PortraitQuery()}", cancellationToken).ConfigureAwait(false);

        return CoverArtJson.ReadImages(body);
    }

    /// <summary>
    /// Fetches the chosen image, ready to hand to <c>Game.AddCustomCover</c>.
    /// </summary>
    /// <remarks>
    /// Read into memory rather than streamed straight in, because the resize reads the stream more
    /// than once and a network stream cannot be rewound. Covers are a few hundred kilobytes.
    /// </remarks>
    internal static async Task<Stream> DownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            // The cdn is public and takes no key. Sending one would hand it to a host that has no
            // use for it.
            using var response = await Helpers.AppHttpClient.Shared.GetAsync(url, timeout.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode == false)
            {
                throw new SteamGridDbException($"The image could not be downloaded ({(int)response.StatusCode}).");
            }

            var memoryStream = new MemoryStream();
            await response.Content.CopyToAsync(memoryStream, timeout.Token).ConfigureAwait(false);
            memoryStream.Position = 0;

            return memoryStream;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
        {
            throw new SteamGridDbException(ResourceHelper.GetString("CoverArt_TimedOut"));
        }
    }

    /// <summary>
    /// Checks a key by using it, and says what was wrong if it did not work.
    /// </summary>
    /// <returns>Null when the key works, otherwise SteamGridDB's own words for why not.</returns>
    /// <remarks>
    /// Worth a round trip before a key is saved. Saving one that does not work is a trap with no
    /// visible way out: the prompt that offers to set a key only appears when there is none, so a
    /// mistyped key means every search fails from then on and the only route back is a settings
    /// page the user was never sent to.
    ///
    /// The cheapest call the api has that still exercises authentication - a search for a word
    /// that certainly matches something. What comes back is thrown away; only whether it came back
    /// matters.
    /// </remarks>
    internal static async Task<string?> ValidateKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await GetAsync($"{BaseUrl}/search/autocomplete/{Uri.EscapeDataString("portal")}", cancellationToken, apiKey).ConfigureAwait(false);

            return null;
        }
        catch (SteamGridDbException err)
        {
            return err.Message;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception err)
        {
            Logger.Error(err);

            return ResourceHelper.GetString("General_Error");
        }
    }

    static async Task<string> GetAsync(string url, CancellationToken cancellationToken, string? apiKeyOverride = null)
    {
        // The override is for checking a key the user has typed but not saved yet. Everything else
        // reads the saved one, so there is still only one place a working key comes from.
        var apiKey = (apiKeyOverride ?? Settings.Instance.SteamGridDbApiKey)?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new SteamGridDbException(ResourceHelper.GetString("CoverArt_NoApiKey"));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // The shared HttpClient's timeout is half an hour, which is right for a dll download and
        // absurd for a search. Without this a stalled network left the picker on "Searching…"
        // indefinitely, and a library scan sat on one game with no way to tell it apart from a slow
        // one. See RequestTimeout for why the cancellation has to be linked rather than replaced.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        HttpResponseMessage response;
        string body;

        try
        {
            response = await Helpers.AppHttpClient.Shared.SendAsync(request, timeout.Token).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
        {
            // Ours, not the user's. HttpClient reports its own timeout as a cancellation too, so
            // without separating them a stalled request was indistinguishable from somebody closing
            // the dialog - and was therefore reported as nothing at all.
            throw new SteamGridDbException(ResourceHelper.GetString("CoverArt_TimedOut"));
        }

        using (response)
        {
            return ReadBody(response, body);
        }
    }

    /// <summary>
    /// How long one request is given before it is called stalled.
    /// </summary>
    /// <remarks>
    /// A search or a grid listing is a small json response; anything beyond this is a network that
    /// is not going to answer. The download of a chosen image gets the same, which is generous for
    /// a few hundred kilobytes.
    /// </remarks>
    static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    static string ReadBody(HttpResponseMessage response, string body)
    {
        // The status code first. This used to read the body's own error before looking at the code,
        // which meant a rate limit or a gateway page - html, not json - was reported as "the
        // response could not be read", and the branch that names a rejected key was unreachable for
        // anything that did not return json.
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new SteamGridDbException(ResourceHelper.GetString("CoverArt_RateLimited"));
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
        {
            // The body may still name which key problem it is, which is more use than the code.
            throw new SteamGridDbException(CoverArtJson.ReadError(body) ?? ResourceHelper.GetString("CoverArt_KeyRejected"));
        }

        if (response.IsSuccessStatusCode == false)
        {
            // The api's own words when it sent any, the code when it did not - rather than the json
            // reader's opinion of a page that was never json.
            throw new SteamGridDbException(
                CoverArtJson.ReadError(body) ?? $"SteamGridDB returned {(int)response.StatusCode}.");
        }

        var error = CoverArtJson.ReadError(body);
        if (error is not null)
        {
            throw new SteamGridDbException(error);
        }

        return body;
    }
}
