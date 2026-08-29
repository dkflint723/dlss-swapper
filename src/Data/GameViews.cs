using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Collections;
using DLSS_Swapper.Interfaces;
using DLSS_Swapper.Messages;
using Microsoft.UI.Xaml.Data;

namespace DLSS_Swapper.Data;

/// <summary>
/// The collection views the games page binds to.
/// </summary>
/// <remarks>
/// <para>
/// These used to live on GameManager, which is why touching GameManager.Instance outside the app
/// used to be impossible: every one of these is a WinUI type, and constructing one with no XAML
/// framework running throws a bare COMException that took the whole singleton with it. They were
/// made lazy to fix that, and now they live somewhere that cannot be reached without a UI at all -
/// GameManager itself is compiled into DLSS.Swapper.Data, which the command line uses and which
/// references nothing that draws.
/// </para>
/// <para>
/// The split is by what a thing needs rather than by what it is about: the registry of games, the
/// scanning and the swapping are here in spirit but not in code, because none of them need a
/// window. What is left here is only the presentation of that list.
/// </para>
/// </remarks>
internal partial class GameViews
{
    public static GameViews Instance { get; } = new GameViews();

    /// <summary>The list itself, which this only ever presents.</summary>
    static GameManager Games => GameManager.Instance;

    ObservableCollection<Game> _allGames => Games.AllGamesCollection;

    bool PassesSharedFilters(Game game, bool hideNonDLSSGames) => Games.PassesSharedFilters(game, hideNonDLSSGames);

    GameViews()
    {
    }

    /// <summary>
    /// The collection views the games page binds to, built on first use rather than in the
    /// constructor.
    /// </summary>
    /// <remarks>
    /// Every one of these is a WinUI type, and constructing one with no XAML framework running
    /// throws a bare COMException with no message. Because they were built in the constructor, that
    /// took the whole singleton with it: touching GameManager.Instance at all outside the app - to
    /// list games from the command line, or to drive a load from a test - failed before a line of
    /// its own code ran. Nothing about the app changes; it reaches these the moment it shows the
    /// games page.
    /// </remarks>
    CollectionViewSource? _groupedGameCollectionViewSource;
    CollectionViewSource? _ungroupedGameCollectionViewSource;

    public CollectionViewSource GroupedGameCollectionViewSource
    {
        get { EnsureViewsBuilt(); return _groupedGameCollectionViewSource!; }
    }

    public CollectionViewSource UngroupedGameCollectionViewSource
    {
        get { EnsureViewsBuilt(); return _ungroupedGameCollectionViewSource!; }
    }

    GameGroup? allGamesGroup;
    GameGroup? favouriteGamesGroup;

    AdvancedCollectionView? _allGamesView;
    AdvancedCollectionView? _favouriteGamesView;

    public AdvancedCollectionView AllGamesView
    {
        get { EnsureViewsBuilt(); return _allGamesView!; }
    }

    public AdvancedCollectionView FavouriteGamesView
    {
        get { EnsureViewsBuilt(); return _favouriteGamesView!; }
    }

    Dictionary<GameLibrary, GameGroup> libraryGameGroups = new Dictionary<GameLibrary, GameGroup>();
    Dictionary<GameLibrary, AdvancedCollectionView> libraryGamesView = new Dictionary<GameLibrary, AdvancedCollectionView>();

    Predicate<object> GetPredicateForAllGames(bool hideNonDLSSGames)
    {
        return (obj) => PassesSharedFilters((Game)obj, hideNonDLSSGames);
    }

    Predicate<object> GetPredicateForFavouriteGames(bool hideNonDLSSGames)
    {
        return (obj) =>
        {
            var game = (Game)obj;
            return game.IsFavourite && PassesSharedFilters(game, hideNonDLSSGames);
        };
    }

    Predicate<object> GetPredicateForLibraryGames(GameLibrary library, bool hideNonDLSSGames)
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
            return game.GameLibrary == library && PassesSharedFilters(game, hideNonDLSSGames);
        };
    }

    bool _viewsBuilt;

    /// <summary>Builds the collection views, once, the first time something asks for one.</summary>
    void EnsureViewsBuilt()
    {
        if (_viewsBuilt)
        {
            return;
        }

        _viewsBuilt = true;

        var FavouriteGamesView = _favouriteGamesView = new AdvancedCollectionView(_allGames, true);
        FavouriteGamesView.Filter = GetPredicateForFavouriteGames(Settings.Instance.HideNonDLSSGames);
        FavouriteGamesView.ObserveFilterProperty(nameof(Game.IsFavourite));
        FavouriteGamesView.ObserveFilterProperty(nameof(Game.HasSwappableItems));
        FavouriteGamesView.ObserveFilterProperty(nameof(Game.IsHidden));
        FavouriteGamesView.SortDescriptions.Add(new SortDescription(nameof(Game.Title), SortDirection.Ascending));

        var AllGamesView = _allGamesView = new AdvancedCollectionView(_allGames, true);
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


        foreach (var gameLibraryEnum in Games.GetGameLibraries(false))
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


        _groupedGameCollectionViewSource = new CollectionViewSource()
        {
            IsSourceGrouped = true,
            Source = groupedList,
            ItemsPath = new PropertyPath("Games"),
        };


        _ungroupedGameCollectionViewSource = new CollectionViewSource()
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

    public ICollectionView GetGameCollection(string? filterText = null)
    {
        // Stored before the filters below are rebuilt, because they read it. All three ways of
        // narrowing now live on this object, so a count taken anywhere applies the same three.
        Games.SearchText = filterText?.Trim() ?? string.Empty;

        // Set before the filters below are rebuilt, because they ask each group whether it is
        // folded and a narrowed one is not. Every way of narrowing lands here: the search text is an
        // argument to this function, and the tab is set on this object immediately before it is
        // called, so nothing can narrow the list without passing through here.
        Games.IsListNarrowed = string.IsNullOrWhiteSpace(filterText) == false
            || Games.ActiveFilter != GameFilter.All
            || Games.DllFilter is not null;
        foreach (var gameGroup in libraryGameGroups.Values)
        {
            gameGroup.Games.IsListNarrowed = IsListNarrowed;
        }

        // Refresh all filters.
        using (FavouriteGamesView.DeferRefresh())
        {
            FavouriteGamesView.Filter = GetPredicateForFavouriteGames(Settings.Instance.HideNonDLSSGames);
        }

        using (AllGamesView.DeferRefresh())
        {
            AllGamesView.Filter = GetPredicateForAllGames(Settings.Instance.HideNonDLSSGames);
        }

        if (Settings.Instance.GroupGameLibrariesTogether)
        {
            // Only refresh libraries when we are going to the grouped view.
            foreach (var keyValuePair in libraryGamesView)
            {
                using (keyValuePair.Value.DeferRefresh())
                {
                    keyValuePair.Value.Filter = GetPredicateForLibraryGames(keyValuePair.Key, Settings.Instance.HideNonDLSSGames);
                }
            }

            return GroupedGameCollectionViewSource.View;
        }
        else
        {
            return UngroupedGameCollectionViewSource.View;
        }
    }
}
