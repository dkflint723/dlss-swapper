using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data.Steam.SteamAPI;
using DLSS_Swapper.Interfaces;
using SQLite;

namespace DLSS_Swapper.Data.Steam;

[Table("steam_game")]
internal partial class SteamGame : Game
{
    public override GameLibrary GameLibrary => GameLibrary.Steam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadyToPlay))]
    [Column("state_flags")]
    public partial SteamStateFlag StateFlags { get; set; }

    public override bool IsReadyToPlay
    {
        get
        {
            const SteamStateFlag allowedFlags = SteamStateFlag.StateFullyInstalled | SteamStateFlag.StateAppRunning;
            return StateFlags != 0 && (StateFlags & ~allowedFlags) == 0;
        }
    }

    public SteamGame()
    {

    }

    public SteamGame(string appId)
    {
        PlatformId = appId;
        SetID();
    }

    protected override async Task UpdateCacheImageAsync()
    {
        // Try get image from the local disk first.
        var localHeaderImagePath = Path.Combine(SteamLibrary.GetInstallPath(), "appcache", "librarycache", $"{PlatformId}_library_600x900.jpg");
        if (File.Exists(localHeaderImagePath))
        {
            using (var fileStream = File.Open(localHeaderImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await ResizeCoverAsync(fileStream).ConfigureAwait(false);
            }
            return;
        }

        // Art somebody set on the game themselves, which for a non-Steam shortcut is the only art
        // there is - Steam has no store page to fetch one from, and never writes one into
        // librarycache. It is also the right answer for an ordinary game whose art has been
        // replaced, since this is the picture Steam itself is showing.
        var gridImagePath = FindGridImagePath();
        if (gridImagePath is not null)
        {
            using (var fileStream = File.Open(gridImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await ResizeCoverAsync(fileStream).ConfigureAwait(false);
            }
            return;
        }

        // A shortcut has nothing on the store to look up. Its id is generated with the top bit set,
        // so it is not an app id at all, and both routes below would spend a request each finding
        // that out - the first of them by overflowing an Int32 parse on the way.
        if (IsNonSteamShortcut())
        {
            Logger.Verbose($"No local art for non-Steam shortcut {PlatformId}, and nothing to ask Steam for.");
            return;
        }

        // Special case for Steamworks redistributable.
        if (PlatformId == "228980")
        {
            await DownloadCoverAsync($"https://steamcdn-a.akamaihd.net/steam/apps/{PlatformId}/header.jpg").ConfigureAwait(false);
            return;
        }

        // Try download via IStoreBrowseService first.
        var didDownload = await DownloadCoverFromIStoreBrowseService();
        if (didDownload == false)
        {
            // Try the old cover system?
            didDownload = await DownloadCoverAsync($"https://steamcdn-a.akamaihd.net/steam/apps/{PlatformId}/library_600x900_2x.jpg").ConfigureAwait(false);

            if (didDownload == false)
            {
                Logger.Error($"Tried to get Steam cover for {PlatformId} but was unable to get it from both old and new Steam CDNs.");
            }
        }
    }

    /// <summary>
    /// Whether this is a game somebody added to Steam rather than one Steam installed.
    /// </summary>
    /// <remarks>
    /// Steam builds a shortcut's id with the top bit set, so every one of them is above what a real
    /// app id can reach. That makes the id itself the test, with nothing else to consult.
    /// </remarks>
    internal bool IsNonSteamShortcut()
    {
        return uint.TryParse(PlatformId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var appId)
            && appId > int.MaxValue;
    }

    /// <summary>
    /// The portrait Steam is showing for this game, if somebody has set one.
    /// </summary>
    /// <remarks>
    /// Kept in each account's own grid folder and named by app id: <c>&lt;id&gt;p.png</c> is the
    /// portrait, which is the shape this app wants. The plain <c>&lt;id&gt;</c> forms are the wide
    /// capsule and are only taken when there is no portrait, because a wide picture in a tall frame
    /// is still better than none.
    /// </remarks>
    string? FindGridImagePath()
    {
        try
        {
            var installPath = SteamLibrary.GetInstallPath();
            if (string.IsNullOrEmpty(installPath))
            {
                return null;
            }

            var userDataPath = Path.Combine(installPath, "userdata");
            if (Directory.Exists(userDataPath) == false)
            {
                return null;
            }

            // Portrait first, across every account, before settling for a wide one anywhere.
            foreach (var suffix in new[] { "p.png", "p.jpg", ".png", ".jpg" })
            {
                foreach (var accountPath in Directory.GetDirectories(userDataPath))
                {
                    var candidate = Path.Combine(accountPath, "config", "grid", $"{PlatformId}{suffix}");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Could not look for Steam grid art for {PlatformId}");
            return null;
        }
    }

    async Task<bool> DownloadCoverFromIStoreBrowseService()
    {
        try
        {
            var getItemsInput = new GetItemsInput();
            getItemsInput.Ids.Add(new StoreItemId() { AppId = Int32.Parse(PlatformId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture) });
            getItemsInput.DataRequest.IncludeAssets = true;

            var jsonPayload = JsonSerializer.Serialize(getItemsInput, SourceGenerationContext.Default.GetItemsInput);
            var payloadUrlEncoded = HttpUtility.UrlEncode(jsonPayload);

            using (var steamApiResponse = await Helpers.AppHttpClient.Shared.GetAsync($"https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?input_json={payloadUrlEncoded}", System.Net.Http.HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                if (steamApiResponse.IsSuccessStatusCode == false)
                {
                    Logger.Error($"Failed to load Steam cover for {PlatformId} from IStoreBrowseService. Status code: {steamApiResponse.StatusCode}");
                    return false;
                }

                using (var responseStream = await steamApiResponse.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var response = JsonSerializer.Deserialize(responseStream, SourceGenerationContext.Default.SteamAPIResponseGetItemsResponse);
                    if (response?.Response?.StoreItems.Any() == true)
                    {
                        // We are only doing one search, so we likely only care for the first item.
                        var storeItem = response.Response.StoreItems[0];

                        if (storeItem.Assets is null)
                        {
                            Logger.Error($"No Assets found for {PlatformId} in the response from IStoreBrowseService.");
                            return false;
                        }

                        if (string.IsNullOrWhiteSpace(storeItem.Assets.AssetUrlFormat))
                        {
                            Logger.Error($"No AssetUrlFormat found for {PlatformId} in the response from IStoreBrowseService.");
                            return false;
                        }

                        // We are only checking LibraryCapsule2x, hopefully it exists for all games
                        if (string.IsNullOrWhiteSpace(storeItem.Assets.LibraryCapsule2x) == false)
                        {
                            // There are 3 different CDNs, I don't lknow what one they will use, so lets try all of them?
                            var cdns = new[]
                            {
                                    "https://shared.fastly.steamstatic.com",
                                    "https://shared.steamstatic.com",
                                    "https://shared.akamai.steamstatic.com"
                                };

                            foreach (var cdn in cdns)
                            {
                                var coverUrl = $"{cdn}/store_item_assets/{storeItem.Assets.AssetUrlFormat.Replace("${FILENAME}", storeItem.Assets.LibraryCapsule2x)}";
                                var didDownloadCover = await DownloadCoverAsync(coverUrl).ConfigureAwait(false);
                                if (didDownloadCover)
                                {
                                    return true;
                                }
                                Logger.Error($"Could not download cover \"{storeItem.Assets.LibraryCapsule2x}\" with CDN {cdn} so trying next.");
                            }
                        }
                    }
                    else
                    {
                        Logger.Error($"No store items found for {PlatformId} in the response from IStoreBrowseService.");
                    }
                }
            }

            Logger.Error($"Tried all known methods to get Steam cover for {PlatformId}, but all had failed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to load Steam cover for {PlatformId} from IStoreBrowseService.");
        }

        return false;
    }

    public override bool UpdateFromGame(Game game)
    {
        var didChange = ParentUpdateFromGame(game);

        if (game is SteamGame steamGame)
        {
            if (StateFlags != steamGame.StateFlags)
            {
                StateFlags = steamGame.StateFlags;
                didChange = true;
            }
        }

        return didChange;
    }
}
