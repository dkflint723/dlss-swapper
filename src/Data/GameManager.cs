using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DLSS_Swapper.Data.BattleNet;
using DLSS_Swapper.Data.Xbox;
using DLSS_Swapper.Interfaces;
using DLSS_Swapper.Messages;
using Windows.System;

namespace DLSS_Swapper.Data;

internal partial class GameManager : ObservableObject
{
    public static GameManager Instance { get; private set; } = new GameManager();

    // Because access to _allGames should be done on the UI thread we have _synchronisedAllGames which
    // will be used for adding/removing/fetching games. _allGames gets updated which will then be reflected
    // to the user.
    List<Game> _synchronisedAllGames = new List<Game>();
    ObservableCollection<Game> _allGames { get; } = new ObservableCollection<Game>();

    [ObservableProperty]
    public partial bool UnknownAssetsFound { get; set; } = false;

    /// <summary>
    /// The list the app's collection views present.
    /// </summary>
    /// <remarks>
    /// Exposed because those views are compiled into the app while this is not - see GameViews.
    /// Still the same collection, so everything that observes it keeps working; nothing else should
    /// be adding to it, which is what AddGame and RemoveGame are for.
    /// </remarks>
    internal ObservableCollection<Game> AllGamesCollection => _allGames;

    /// <summary>
    /// Asks whatever is showing the games to bring one into view. Null when nothing is.
    /// </summary>
    /// <remarks>
    /// Set by the games page. This used to reach through the window to the page directly, which is
    /// the sort of line that stops a list of games being usable without one.
    /// </remarks>
    internal static Action<Game>? ScrollToGameRequested { get; set; }

    List<UnknownGameAsset> _unknownGameAssets { get; } = new List<UnknownGameAsset>();

    object gameLock = new object();
    object unknownGameAsseetLock = new object();


    /// <summary>
    /// Everything the three views agree on: hidden games, the active tab, the search text, and
    /// whether games without upscalers are shown.
    /// </summary>
    /// <remarks>
    /// One copy rather than three. Written out per view, the same change had to be made in three
    /// places and twice was made in only one: every tab showed the same list when games were
    /// grouped by library, and the hidden tab counted its games and then showed none. Each view now
    /// adds only the clause that is actually its own.
    /// </remarks>
    /// <summary>
    /// What is in the search box, or empty when it is empty.
    /// </summary>
    /// <remarks>
    /// Here, next to the tab and the dll filter, because it is the third thing that narrows the
    /// list and the only one that used to live somewhere else. While it did, the tab counts and the
    /// review button were computed without it: searching "final" left the list showing four games
    /// under a tab still reading twenty four.
    /// </remarks>
    public string SearchText { get; internal set; } = string.Empty;

    /// <summary>Whether a game survives the search box. The one place that rule is written.</summary>
    public bool MatchesSearch(Game game)
    {
        return string.IsNullOrEmpty(SearchText)
            || game.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    internal bool PassesSharedFilters(Game game, bool hideNonDLSSGames)
    {
        // Hidden games are GameFilters' business now, along with the counts. They were excluded
        // here and only here, which is how "All games" came to count games it would not show.
        if (GameFilters.Matches(game, ActiveFilter, hideNonDLSSGames) == false)
        {
            return false;
        }

        // Here rather than in GameFilters, because it is not one of the tabs and must not become a
        // fifth one: it narrows whichever tab is selected instead of replacing it.
        if (DllFilter is not null && DllFilter.Matches(game) == false)
        {
            return false;
        }

        return MatchesSearch(game);
    }

    /// <summary>
    /// Raised whenever games are added, removed or cleared.
    /// </summary>
    /// <remarks>
    /// So anything showing a count can recompute rather than being told to by every caller that
    /// might have changed something. Games load from a fire and forget on the games page, long
    /// after the window and its sidebar are built, so a count taken at construction is always taken
    /// against an empty library.
    /// </remarks>
    public event EventHandler? GamesChanged;

    /// <summary>
    /// Which filter tab the games page is on. Session only, so opening the app always shows
    /// everything rather than a subset the user set once and forgot.
    /// </summary>
    public GameFilter ActiveFilter { get; set; } = GameFilter.All;

    /// <summary>
    /// Set when the user has asked which games are using one particular dll, and null the rest of
    /// the time.
    /// </summary>
    /// <remarks>
    /// Composes with the tab rather than replacing it, so "which of these is behind" is still
    /// askable while it is on. Session only, like the tab: this arrives by a deliberate click from
    /// another page and should never be what the app opens on.
    /// </remarks>
    public DllFilter? DllFilter { get; set; }

    /// <summary>
    /// Whether the page is showing a subset of the library, by search, by tab or by dll.
    /// </summary>
    /// <remarks>
    /// Worked out in <see cref="GetGameCollection"/>, which is the one place both are known, and
    /// read back from there rather than recomputed by anyone who needs it.
    /// </remarks>
    public bool IsListNarrowed { get; internal set; }

    private GameManager()
    {
        // Everything else this used to do is in EnsureViewsBuilt. This is the only part that is
        // safe with no XAML framework running, and it is the only part a headless caller needs.
        _allGames.CollectionChanged += (sender, args) =>
        {
            RaiseGamesChanged();
        };
    }

    /// <summary>
    /// Every cached game's recorded dlls, read in one go and handed out as each game asks.
    /// </summary>
    /// <remarks>
    /// Null except during a cache load. Each game used to run its own <c>WHERE id = ?</c> against
    /// game_asset, taking the database mutex for each - one query and one lock per game, in a phase
    /// that is the reason the list is not on screen yet.
    /// </remarks>
    Dictionary<string, List<GameAsset>>? _prefetchedGameAssets;

    /// <summary>
    /// This game's recorded dlls out of the prefetch, or null if there is no prefetch to read.
    /// </summary>
    /// <remarks>
    /// Null and empty mean different things here. Null is "ask the database yourself"; an empty
    /// list is "the prefetch covered you and you have none", which is most of a library.
    /// </remarks>
    internal List<GameAsset>? PrefetchedAssetsFor(string gameId)
    {
        var prefetched = _prefetchedGameAssets;

        if (prefetched is null)
        {
            return null;
        }

        return prefetched.TryGetValue(gameId, out var assets) ? assets : new List<GameAsset>();
    }

    public async Task LoadGamesFromCacheAsync()
    {
        UnknownAssetsFound = false;
        _unknownGameAssets.Clear();

        try
        {
            _prefetchedGameAssets = await ReadAllGameAssetsAsync().ConfigureAwait(false);

            foreach (var gameLibraryEnum in GameManager.Instance.GetGameLibraries(true))
            {
                var gameLibrary = IGameLibrary.GetGameLibrary(gameLibraryEnum);
                if (gameLibrary.IsEnabled)
                {
                    await gameLibrary.LoadGamesFromCacheAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            // Dropped as soon as the load is over. Anything reading assets after this point wants
            // what is in the database now, not what was there when the app started.
            _prefetchedGameAssets = null;
        }

        await LoadDllPinsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Hands every loaded game its pins.
    /// </summary>
    /// <remarks>
    /// After the cache load rather than during it, because pins belong to games and the games have
    /// to exist first. A game scanned into the library later has no pins by definition — a pin is
    /// only ever created from that game's own page.
    /// </remarks>
    async Task LoadDllPinsAsync()
    {
        try
        {
            List<GameDllPin> allPins;
            using (await Database.Instance.Mutex.LockAsync())
            {
                allPins = await Database.Instance.Connection.Table<GameDllPin>().ToListAsync().ConfigureAwait(false);
            }

            if (allPins.Count == 0)
            {
                return;
            }

            foreach (var game in GetSynchronisedGamesListCopy())
            {
                var pinsForGame = allPins.Where(x => x.GameId == game.ID).ToList();
                if (pinsForGame.Count > 0)
                {
                    game.SetDllPins(pinsForGame);
                }
            }
        }
        catch (Exception err)
        {
            // A failed read costs the pins for this session, not the library.
            Logger.Error(err);
        }
    }

    /// <summary>Every row of game_asset, grouped by the game it belongs to.</summary>
    /// <remarks>
    /// Returns null rather than throwing, so a failure here costs the batching and nothing else -
    /// each game falls back to asking for its own.
    /// </remarks>
    static async Task<Dictionary<string, List<GameAsset>>?> ReadAllGameAssetsAsync()
    {
        try
        {
            List<GameAsset> allAssets;

            using (await Database.Instance.Mutex.LockAsync())
            {
                allAssets = await Database.Instance.Connection.Table<GameAsset>().ToListAsync().ConfigureAwait(false);
            }

            var grouped = new Dictionary<string, List<GameAsset>>();

            foreach (var gameAsset in allAssets)
            {
                if (grouped.TryGetValue(gameAsset.Id, out var assets) == false)
                {
                    assets = new List<GameAsset>();
                    grouped[gameAsset.Id] = assets;
                }

                assets.Add(gameAsset);
            }

            return grouped;
        }
        catch (Exception err)
        {
            Logger.Error(err);

            return null;
        }
    }

    public async Task LoadGamesAsync(bool forceNeedsProcessing = false)
    {
        var tasks = new List<Task<List<Game>>>();
        if (forceNeedsProcessing == true)
        {
            lock (unknownGameAsseetLock)
            {
                _unknownGameAssets.Clear();
            }
        }
        foreach (var gameLibraryEnum in GameManager.Instance.GetGameLibraries(true))
        {
            var gameLibrary = IGameLibrary.GetGameLibrary(gameLibraryEnum);
            if (gameLibrary.IsEnabled)
            {
                // Started on the thread pool rather than called straight. A ListGamesAsync runs a
                // long way before it reaches its first await - Steam parses libraryfolders.vdf and
                // reads every .acf, Xbox enumerates every installed package and loads an XML per
                // game folder - and this method is reached on the UI thread, so all of that ran
                // there. Calling an async method does not move its beginning off the caller's
                // thread; only this does.
                tasks.Add(Task.Run(() => gameLibrary.ListGamesAsync(forceNeedsProcessing)));
            }
        }

        // Add games to the game library when the tasks is completed.
        while (tasks.Any())
        {
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            List<Game> games;

            try
            {
                // Awaited rather than read through .Result. That rethrows wrapped in an
                // AggregateException, out of this loop, which abandoned every library still running
                // - never observed, never added, and the loading flags left on. An unreadable drive
                // in one library took the rest of the libraries with it.
                games = await completedTask;
            }
            catch (Exception err)
            {
                Logger.Error(err, "A game library could not be listed. The others are unaffected.");
                continue;
            }

            foreach (var game in games)
            {
                AddGame(game);
            }
        }
    }

    /// <summary>
    /// Recomputes which games have a newer dll available.
    /// </summary>
    /// <remarks>
    /// Games are restored from cache before the manifest finishes loading, so on a cold start the
    /// first pass has nothing to compare against. This gets called again once records are in.
    /// </remarks>
    public void RefreshUpdateAvailable()
    {
        foreach (var game in GetSynchronisedGamesListCopy())
        {
            game.RefreshUpdateAvailable();
        }

        // Whether a game is behind is only decided once the manifest has loaded, which is long
        // after the games themselves. Anything counting them has to be told, or it keeps the
        // answer it computed while every game still looked up to date.
        RaiseGamesChanged();
    }

    /// <summary>
    /// Says that something about a game has changed which the counts and tabs are computed from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GamesChanged"/> otherwise fires only when the list gains or loses a game, so
    /// every mutation that changes a game rather than the list - hiding it, favouriting it, telling
    /// it to skip updates, finishing a batch of swaps - left every count describing the library as
    /// it was a moment earlier.
    /// </para>
    /// <para>
    /// The lists themselves were never the problem: the views observe the individual properties and
    /// re-filter on their own. It is the numbers beside them that nobody told, which is how a tab
    /// came to read 23 over a list of 22, and how "Review 7 updates" survived the batch that
    /// emptied it and then did nothing when pressed.
    /// </para>
    /// </remarks>
    public void NotifyGamesChanged()
    {
        RaiseGamesChanged();
    }

    bool _gamesChangedIsPending;

    /// <summary>
    /// Raises <see cref="GamesChanged"/> once for a run of changes rather than once per change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both subscribers answer it by walking the whole library on the UI thread: the games page
    /// rebuilds four filter tabs, and the sidebar re-summarises every game, its assets and every
    /// known dll. Raising that per game made loading a library quadratic in its size, and it was
    /// paid exactly while the list was trying to lay itself out - so the games appeared in visible
    /// stutters, and got worse the more of them there were.
    /// </para>
    /// <para>
    /// Enqueued on the dispatcher rather than raised, and deliberately not through
    /// <c>RunOnUIThread</c>: that runs inline when it is already on the UI thread, which is exactly
    /// where the adds happen, so nothing would ever be deferred. Going straight to the queue means
    /// the whole run of adds lands first and the recount happens once, after them.
    /// </para>
    /// <para>
    /// Nothing awaits this event, and both subscribers only recompute what they display, so
    /// arriving a frame later than the change costs nothing.
    /// </para>
    /// </remarks>
    void RaiseGamesChanged()
    {
        if (_gamesChangedIsPending)
        {
            return;
        }

        var dispatcher = UiThread.Dispatcher;

        if (dispatcher is null)
        {
            // Before there is a window there is nothing to coalesce against, and nothing drawing
            // that could be made to stutter.
            GamesChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _gamesChangedIsPending = true;

        var didEnqueue = dispatcher.TryEnqueue(() =>
        {
            _gamesChangedIsPending = false;
            GamesChanged?.Invoke(this, EventArgs.Empty);
        });

        if (didEnqueue == false)
        {
            // A queue that will not take work is shutting down, but a dropped recount would leave
            // the counts wrong for as long as the window lives, so raise it here instead.
            _gamesChangedIsPending = false;
            GamesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public List<Game> GetSynchronisedGamesListCopy()
    {
        lock (gameLock)
        {
            var list = new List<Game>(_synchronisedAllGames);
            return list;
        }
    }



    public Game AddGame(Game game, bool scrollIntoView = false)
    {
        lock (gameLock)
        {
            if (_synchronisedAllGames.Contains(game) == true)
            {
                // This probably checks the game collection twice looking for the game.
                // We could do away with this, but in theory this if is never hit
                var oldGame = _synchronisedAllGames.First(x => x.Equals(game));

                UiThread.Run(() =>
                {
                    oldGame.UpdateFromGame(game);
                });

                Debug.WriteLine($"Reusing old game: {game.Title}");
                return oldGame;
            }
            else
            {
                Debug.WriteLine($"Adding new game: {game.Title}");

                _synchronisedAllGames.Add(game);

                UiThread.Run(() =>
                {
                    _allGames.Add(game);

                    if (scrollIntoView)
                    {
                        ScrollToGameRequested?.Invoke(game);
                    }
                });

                return game;
            }
        }
    }

    public void RemoveGame(Game game)
    {
        lock (gameLock)
        {
            _synchronisedAllGames.Remove(game);

            UiThread.Run(() =>
            {
                _allGames.Remove(game);
            });
        }
    }

    public void RemoveAllGames()
    {
        lock (gameLock)
        {
            // TODO: Cancel loading of games here
            _synchronisedAllGames.Clear();

            UiThread.Run(() =>
            {
                _allGames.Clear();
            });
        }
    }

    public TGame? GetGame<TGame>(string platformId) where TGame : Game
    {
        lock (gameLock)
        {
            foreach (var game in _synchronisedAllGames)
            {
                if (game is TGame platformGame)
                {
                    if (game.PlatformId == platformId)
                    {
                        return platformGame;
                    }
                }
            }
        }

        return null;
    }

    public List<TGame> GetGames<TGame>() where TGame : Game
    {
        lock (gameLock)
        {
            var games = new List<TGame>();
            foreach (var game in _synchronisedAllGames)
            {
                if (game is TGame tGame)
                {
                    games.Add(tGame);
                }
            }
            return games;
        }
    }


    public bool CheckIfGameIsAdded(string installPath)
    {
        lock (gameLock)
        {
            foreach (var game in _synchronisedAllGames)
            {
                if (game.InstallPath?.Equals(installPath, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
        }
        return false;
    }


    public void AddUnknownGameAssets(GameLibrary gameLibrary, string gameTitle, List<GameAsset> gameAssets)
    {
        lock (unknownGameAsseetLock)
        {
            if (UnknownAssetsFound == false)
            {
                UiThread.Run(() =>
                {
                    UnknownAssetsFound = true;
                });
            }

            foreach (var gameAsset in gameAssets)
            {
                _unknownGameAssets.Add(new UnknownGameAsset(gameLibrary, gameTitle, gameAsset));
            }
        }
    }

    public List<UnknownGameAsset> GetUnknownGameAssets()
    {
        var unknownGameAssets = new List<UnknownGameAsset>();

        lock (unknownGameAsseetLock)
        {
            unknownGameAssets.AddRange(_unknownGameAssets);
        }

        return unknownGameAssets;
    }

    public GameLibrarySettings? GetGameLibrarySettings(GameLibrary gameLibrary)
    {
        return Settings.Instance.GameLibrarySettings.FirstOrDefault(x => x.GameLibrary == gameLibrary);
    }

    public List<GameLibrary> GetGameLibraries(bool onlyEnabled)
    {
        var gameLibrariesToReturn = new List<GameLibrary>();

        foreach (var gameLibrarySetting in Settings.Instance.GameLibrarySettings)
        {
            if (gameLibrarySetting.IsEnabled == false && onlyEnabled == true)
            {
                continue;
            }

            gameLibrariesToReturn.Add(gameLibrarySetting.GameLibrary);
        }

        return gameLibrariesToReturn;

    }

    public bool CanLaunchGame(Game game)
    {
        // Xbox App games are only valid if ApplicationId is loaded.
        if (game.GameLibrary == GameLibrary.XboxApp)
        {
            if (game is XboxGame xboxGame && string.IsNullOrWhiteSpace(xboxGame.ApplicationId) == false)
            {
                return true;
            }
        }

        // We can only launch Battle.net games if Battle.net client is installed
        if (game.GameLibrary == GameLibrary.BattleNet)
        {
            if (game is BattleNetGame battleNetGame)
            {
                if (string.IsNullOrWhiteSpace(battleNetGame.LauncherId) == false)
                {
                    return true;
                }
            }
        }

        return game.GameLibrary switch
        {
            GameLibrary.Steam => true,
            GameLibrary.EpicGamesStore => true,
            GameLibrary.EAApp => true,
            _ => false,
        };
    }

    public async Task LaunchGameAsync(Game game)
    {
        if (CanLaunchGame(game) == false)
        {
            Logger.Error($"Cannot launch game {game.Title} from {game.GameLibrary}");
            return;
        }

        if (game.GameLibrary == GameLibrary.Steam)
        {
            await Launcher.LaunchUriAsync(new Uri($"steam://rungameid/{game.PlatformId}"));
        }
        else if (game.GameLibrary == GameLibrary.EpicGamesStore)
        {
            var installPathString = Uri.EscapeDataString(game.InstallPath);
            await Launcher.LaunchUriAsync(new Uri($"com.epicgames.launcher://apps/{installPathString}?action=launch&silent=true"));
        }
        else if (game.GameLibrary == GameLibrary.EAApp)
        {
            await Launcher.LaunchUriAsync(new Uri($"origin2://game/launch?offerIds={game.PlatformId}"));
        }
        else if (game.GameLibrary == GameLibrary.XboxApp)
        {
            if (game is XboxGame xboxGame)
            {
                var launchCode = $"shell:appsFolder\\{xboxGame.PlatformId}!{xboxGame.ApplicationId}";
                Process.Start(new ProcessStartInfo("explorer.exe", launchCode) { UseShellExecute = true });
            }
        }
        else if (game.GameLibrary == GameLibrary.BattleNet)
        {
            if (game is BattleNetGame battleNetGame && File.Exists(BattleNetLibrary.Instance.ClientPath))
            {
                Process.Start(new ProcessStartInfo(BattleNetLibrary.Instance.ClientPath,  $"--exec=\"launch {battleNetGame.LauncherId}\"") { UseShellExecute = true });
            }
        }
    }
}
