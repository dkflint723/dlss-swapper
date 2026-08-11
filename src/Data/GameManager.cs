using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Collections;
using DLSS_Swapper.Data.BattleNet;
using DLSS_Swapper.Data.Xbox;
using DLSS_Swapper.Interfaces;
using DLSS_Swapper.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
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

    public CollectionViewSource GroupedGameCollectionViewSource { get; init; }
    public CollectionViewSource UngroupedGameCollectionViewSource { get; init; }

    [ObservableProperty]
    public partial bool UnknownAssetsFound { get; set; } = false;

    List<UnknownGameAsset> _unknownGameAssets { get; } = new List<UnknownGameAsset>();

    object gameLock = new object();
    object unknownGameAsseetLock = new object();

    GameGroup allGamesGroup;
    GameGroup favouriteGamesGroup;

    public AdvancedCollectionView AllGamesView { get; init; }
    public AdvancedCollectionView FavouriteGamesView { get; init; }

    Dictionary<GameLibrary, GameGroup> libraryGameGroups = new Dictionary<GameLibrary, GameGroup>();
    Dictionary<GameLibrary, AdvancedCollectionView> libraryGamesView = new Dictionary<GameLibrary, AdvancedCollectionView>();


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
    bool PassesSharedFilters(Game game, bool hideNonDLSSGames, string? filterText)
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

        return string.IsNullOrEmpty(filterText)
            || game.Title.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    Predicate<object> GetPredicateForAllGames(bool hideNonDLSSGames, string? filterText = null)
    {
        return (obj) => PassesSharedFilters((Game)obj, hideNonDLSSGames, filterText);
    }

    Predicate<object> GetPredicateForFavouriteGames(bool hideNonDLSSGames, string? filterText = null)
    {
        return (obj) =>
        {
            var game = (Game)obj;
            return game.IsFavourite && PassesSharedFilters(game, hideNonDLSSGames, filterText);
        };
    }

    Predicate<object> GetPredicateForLibraryGames(GameLibrary library, bool hideNonDLSSGames, string? filterText = null)
    {
        return (obj) =>
        {
            // Read when the filter runs, not when it is built, so folding a section only has to ask
            // its view to filter again. The group does not exist yet the first time this is built.
            if (libraryGameGroups.TryGetValue(library, out var gameGroup) && gameGroup.IsFolded)
            {
                return false;
            }

            var game = (Game)obj;
            return game.GameLibrary == library && PassesSharedFilters(game, hideNonDLSSGames, filterText);
        };
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
    public bool IsListNarrowed { get; private set; }

    private GameManager()
    {
        _allGames.CollectionChanged += (sender, args) =>
        {
            GamesChanged?.Invoke(this, EventArgs.Empty);
        };

        FavouriteGamesView = new AdvancedCollectionView(_allGames, true);
        FavouriteGamesView.Filter = GetPredicateForFavouriteGames(Settings.Instance.HideNonDLSSGames);
        FavouriteGamesView.ObserveFilterProperty(nameof(Game.IsFavourite));
        FavouriteGamesView.ObserveFilterProperty(nameof(Game.HasSwappableItems));
        FavouriteGamesView.ObserveFilterProperty(nameof(Game.IsHidden));
        FavouriteGamesView.SortDescriptions.Add(new SortDescription(nameof(Game.Title), SortDirection.Ascending));

        AllGamesView = new AdvancedCollectionView(_allGames, true);
        AllGamesView.Filter = GetPredicateForAllGames(Settings.Instance.HideNonDLSSGames);
        AllGamesView.ObserveFilterProperty(nameof(Game.HasSwappableItems));
        AllGamesView.ObserveFilterProperty(nameof(Game.IsHidden));
        AllGamesView.SortDescriptions.Add(new SortDescription(nameof(Game.Title), SortDirection.Ascending));


        allGamesGroup = new GameGroup(string.Empty, null, AllGamesView);
        favouriteGamesGroup = new GameGroup("Favourites", null, FavouriteGamesView);

        var groupedList = new ObservableCollection<GameGroup>()
        {
            favouriteGamesGroup,
        };

        var ungroupedList = new List<GameGroup>()
        {
            favouriteGamesGroup,
            allGamesGroup,
        };


        foreach (var gameLibraryEnum in GetGameLibraries(false))
        {
            var gameLibrary = IGameLibrary.GetGameLibrary(gameLibraryEnum);

            var gameView = new AdvancedCollectionView(_allGames, true);
            gameView.Filter = GetPredicateForLibraryGames(gameLibraryEnum, Settings.Instance.HideNonDLSSGames);
            gameView.ObserveFilterProperty(nameof(Game.HasSwappableItems));
            gameView.ObserveFilterProperty(nameof(Game.IsHidden));
            gameView.SortDescriptions.Add(new SortDescription(nameof(Game.Title), SortDirection.Ascending));

            libraryGamesView[gameLibraryEnum] = gameView;

            var gameGroup = new GameGroup(gameLibrary.Name, gameLibrary.GameLibrary, gameView);
            groupedList.Add(gameGroup);
            libraryGameGroups[gameLibraryEnum] = gameGroup;
        }


        GroupedGameCollectionViewSource = new CollectionViewSource()
        {
            IsSourceGrouped = true,
            Source = groupedList,
            ItemsPath = new PropertyPath("Games"),
        };


        UngroupedGameCollectionViewSource = new CollectionViewSource()
        {
            IsSourceGrouped = true,
            Source = ungroupedList,
            ItemsPath = new PropertyPath("Games"),
        };


        WeakReferenceMessenger.Default.Register<GameLibrariesOrderChangedMessage>(this, (sender, message) =>
        {
            var groupedGameLibraryList = groupedList.ToList();

            groupedList.Clear();

            // Add favourites
            groupedList.Add(groupedGameLibraryList[0]);
            groupedGameLibraryList.RemoveAt(0);


            // Add each of the items in the order that is from settings.
            foreach (var gameLibrarySetting in Settings.Instance.GameLibrarySettings)
            {
                var groupedItem = groupedGameLibraryList.Single(x => x.GameLibrary == gameLibrarySetting.GameLibrary);
                groupedList.Add(groupedItem);
                groupedGameLibraryList.Remove(groupedItem);
            }

            if (groupedGameLibraryList.Count > 0)
            {
                Logger.Error($"Somehow extra grouped items were left over. {string.Join(", ", groupedGameLibraryList)}");
            }
        });

    }

    public async Task LoadGamesFromCacheAsync()
    {
        UnknownAssetsFound = false;
        _unknownGameAssets.Clear();

        foreach (var gameLibraryEnum in GameManager.Instance.GetGameLibraries(true))
        {
            var gameLibrary = IGameLibrary.GetGameLibrary(gameLibraryEnum);
            if (gameLibrary.IsEnabled)
            {
                await gameLibrary.LoadGamesFromCacheAsync().ConfigureAwait(false);
            }
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
                tasks.Add(gameLibrary.ListGamesAsync(forceNeedsProcessing));
            }
        }

        // Add games to the game library when the tasks is completed.
        while (tasks.Any())
        {
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            foreach (var game in completedTask.Result)
            {
                AddGame(game);
            }
        }
    }

    public ICollectionView GetGameCollection(string? filterText = null)
    {
        // Set before the filters below are rebuilt, because they ask each group whether it is
        // folded and a narrowed one is not. Both ways of narrowing land here: the search text is an
        // argument to this function, and the tab is set on this object immediately before it is
        // called, so nothing can narrow the list without passing through here.
        IsListNarrowed = string.IsNullOrWhiteSpace(filterText) == false
            || ActiveFilter != GameFilter.All
            || DllFilter is not null;
        foreach (var gameGroup in libraryGameGroups.Values)
        {
            gameGroup.IsListNarrowed = IsListNarrowed;
        }

        // Refresh all filters.
        using (FavouriteGamesView.DeferRefresh())
        {
            FavouriteGamesView.Filter = GetPredicateForFavouriteGames(Settings.Instance.HideNonDLSSGames, filterText);
        }

        using (AllGamesView.DeferRefresh())
        {
            AllGamesView.Filter = GetPredicateForAllGames(Settings.Instance.HideNonDLSSGames, filterText);
        }

        if (Settings.Instance.GroupGameLibrariesTogether)
        {
            // Only refresh libraries when we are going to the grouped view.
            foreach (var keyValuePair in libraryGamesView)
            {
                using (keyValuePair.Value.DeferRefresh())
                {
                    keyValuePair.Value.Filter = GetPredicateForLibraryGames(keyValuePair.Key, Settings.Instance.HideNonDLSSGames, filterText);
                }
            }

            return GroupedGameCollectionViewSource.View;
        }
        else
        {
            return UngroupedGameCollectionViewSource.View;
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
        GamesChanged?.Invoke(this, EventArgs.Empty);
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

                App.CurrentApp.RunOnUIThread(() =>
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

                App.CurrentApp.RunOnUIThread(() =>
                {
                    _allGames.Add(game);

                    if (scrollIntoView)
                    {
                        App.CurrentApp.MainWindow.GameGridPage?.ScrollToGame(game);
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

            App.CurrentApp.RunOnUIThread(() =>
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

            App.CurrentApp.RunOnUIThread(() =>
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
                App.CurrentApp.RunOnUIThread(() =>
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
