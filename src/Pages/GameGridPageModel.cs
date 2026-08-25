using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Builders;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.SteamGridDb;
using DLSS_Swapper.Helpers;
using CommunityToolkit.Mvvm.Messaging;
using DLSS_Swapper.Messages;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Windows.System;

namespace DLSS_Swapper.Pages;

public enum GameGridViewType
{
    GridView,
    ListView,
}

public partial class GameGridPageModel : ObservableObject
{
    GameGridPage gameGridPage;

    [ObservableProperty]
    public partial Game? SelectedGame { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    public partial bool IsGameListLoading { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    public partial bool IsDLSSLoading { get; set; } = true;

    public bool IsLoading => (IsGameListLoading || IsDLSSLoading);

    [ObservableProperty]
    public partial ICollectionView? CurrentCollectionView { get; set; } = null;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridViewArtHeight))]
    [NotifyPropertyChangedFor(nameof(GridViewCardHeight))]
    public partial int GridViewItemWidth { get; set; } = Settings.Instance.GridViewItemWidth;

    /// <summary>The cover art, keeping the 2:3 shape of the 400x600 art the cache holds.</summary>
    public int GridViewArtHeight => (int)(GridViewItemWidth * 1.5);

    /// <summary>
    /// The caption below the art: a title line, a status line, and the padding around them.
    /// </summary>
    /// <remarks>
    /// A constant rather than Auto, and deliberately not scaled with the card width. A GridView
    /// takes one cell size for the whole grid from the first item it measures, so a caption sized
    /// by its own text would size every card in every section from whichever game happened to be
    /// measured first. The text does not scale with the zoom either, so the block cannot.
    /// </remarks>
    public const int GridViewCaptionHeight = 76;

    public int GridViewCardHeight => GridViewArtHeight + GridViewCaptionHeight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameGridViewIcon))]
    [NotifyPropertyChangedFor(nameof(IsGridView))]
    [NotifyPropertyChangedFor(nameof(IsListView))]
    public partial GameGridViewType GameGridViewType { get; set; } = Settings.Instance.GameGridViewType;

    /// <summary>So the open View menu marks the one that is current, rather than leaving it to a glyph.</summary>
    public bool IsGridView => GameGridViewType == GameGridViewType.GridView;

    public bool IsListView => GameGridViewType == GameGridViewType.ListView;

    public FontIcon GameGridViewIcon => GameGridViewType switch
    {
        GameGridViewType.GridView => new FontIcon() { Glyph = "\xF0E2" },
        GameGridViewType.ListView => new FontIcon() { Glyph = "\xE8FD" },
        _ => new FontIcon() { },
    };

    public GameGridPageModelTranslationProperties TranslationProperties { get; } = new GameGridPageModelTranslationProperties();

    /// <summary>The filter tabs, with their counts. Rebuilt whenever the library changes.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<GameFilterTab> FilterTabs { get; set; } = [];

    /// <summary>Reads as "Review 7 updates". Hidden when nothing is behind.</summary>
    [ObservableProperty]
    public partial string ReviewUpdatesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility ReviewUpdatesVisibility { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// Recomputes the tab counts and the review button.
    /// </summary>
    /// <remarks>
    /// The counts come from the same rule that decides what each tab shows, so a tab reading "3"
    /// cannot open onto four games.
    /// </remarks>
    /// <summary>
    /// The games the page is currently about.
    /// </summary>
    /// <remarks>
    /// The whole library, unless a dll filter is on, in which case it is the games using that file.
    /// Every count the page shows and every button that acts on "the games" has to come from here:
    /// with the tab counts narrowed and the review button still reading the full library, the
    /// button said "Review 3 updates" and opened a sheet holding twelve.
    ///
    /// Not the tab, though. The tab is a further narrowing that each tab count applies for itself —
    /// including the Hidden tab, which is the one place a hidden game is meant to appear.
    /// </remarks>
    static List<Game> GamesOnThePage()
    {
        var games = GameManager.Instance.GetSynchronisedGamesListCopy();
        var dllFilter = GameManager.Instance.DllFilter;

        if (dllFilter is not null)
        {
            games = games.Where(dllFilter.Matches).ToList();
        }

        // The search box narrows the list the same way the dll filter does, so it has to narrow
        // this the same way too. Without it the tabs above a search for "final" still counted the
        // whole library, and "Update all games" would have written to games the search had hidden.
        return games.Where(GameManager.Instance.MatchesSearch).ToList();
    }

    public void RefreshFilterTabs()
    {
        var games = GamesOnThePage();
        var active = GameManager.Instance.ActiveFilter;

        // Same setting the views apply, so a count cannot include a game the list is hiding.
        var hideNonDLSS = Settings.Instance.HideNonDLSSGames;

        FilterTabs =
        [
            GameFilterTab.For(GameFilter.All, "GamesPage_Filter_All", games, active, hideNonDLSS),
            GameFilterTab.For(GameFilter.HasUpdate, "GamesPage_Filter_HaveUpdate", games, active, hideNonDLSS),
            GameFilterTab.For(GameFilter.MissingBackup, "GamesPage_Filter_MissingOriginal", games, active, hideNonDLSS),
            GameFilterTab.For(GameFilter.Hidden, "GamesPage_Filter_Hidden", games, active, hideNonDLSS),
        ];

        var behind = GameFilters.Count(games, GameFilter.HasUpdate, hideNonDLSS);

        // "updates for N games", not "N updates". This number counts games, and the sheet it opens
        // counts files - so a button reading "Review 2 updates" opened onto "Update 9 files across
        // 2 games?" and the number appeared to change on the way.
        ReviewUpdatesText = behind == 1
            ? ResourceHelper.GetString("GamesPage_ReviewUpdatesOne")
            : ResourceHelper.GetFormattedResourceTemplate("GamesPage_ReviewUpdatesTemplate", behind);
        ReviewUpdatesVisibility = behind > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    [RelayCommand]
    void SelectFilter(GameFilterTab? tab)
    {
        if (tab is not null)
        {
            ShowFilter(tab.Filter);
        }
    }

    /// <summary>
    /// Rebuilds the views against the current settings.
    /// </summary>
    /// <remarks>
    /// The predicates read settings when they are built, not when they run, so changing one has no
    /// effect until they are made again.
    /// </remarks>
    public void ReapplyFilters()
    {
        ShowGameCollection();
        RefreshFilterTabs();
    }

    /// <summary>Switches the page to a filter tab. Also used by the sidebar's backup card.</summary>
    public void ShowFilter(GameFilter filter)
    {
        GameManager.Instance.ActiveFilter = filter;
        ShowGameCollection();
        RefreshFilterTabs();
    }

    /// <summary>What the page is showing while a dll filter is on, and empty when there is none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DllFilterVisibility))]
    public partial string DllFilterLabel { get; set; } = string.Empty;

    public Visibility DllFilterVisibility => string.IsNullOrEmpty(DllFilterLabel)
        ? Visibility.Collapsed
        : Visibility.Visible;

    /// <summary>
    /// Narrows the page to the games using one dll, arrived at from the upscalers page.
    /// </summary>
    /// <remarks>
    /// Lands on "All games" on purpose. The tab is whatever it was last left on, and arriving into
    /// "Hidden" narrowed to one dll is a page showing nothing for two reasons at once, only one of
    /// which the user asked for.
    /// </remarks>
    public void ShowGamesUsingDll(DllFilter dllFilter)
    {
        GameManager.Instance.DllFilter = dllFilter;
        DllFilterLabel = dllFilter.Label;
        ShowFilter(GameFilter.All);
    }

    /// <summary>
    /// Puts the whole library back.
    /// </summary>
    /// <remarks>
    /// The reason the filter has a visible label at all: a page quietly showing three of twenty
    /// three games, with nothing on screen saying why, is indistinguishable from a broken library.
    /// </remarks>
    [RelayCommand]
    void ClearDllFilter()
    {
        GameManager.Instance.DllFilter = null;
        DllFilterLabel = string.Empty;
        ShowGameCollection();
        RefreshFilterTabs();
    }

    /// <summary>
    /// Points the page at the games collection, however it was asked for.
    /// </summary>
    /// <remarks>
    /// One route rather than four copies of the same two calls, which is how the search box came to
    /// have its own version that skipped the filter text when it was empty.
    /// </remarks>
    void ShowGameCollection(string? searchText = null)
    {
        CurrentCollectionView = GameManager.Instance.GetGameCollection(searchText);

        // After the collection, which is what records the search text. Both the tab counts and the
        // empty state read it from there rather than from a copy kept here.
        RefreshFilterTabs();
        RefreshEmptyState();
    }

    /// <summary>What the content area says when it is showing nothing, or null when it is not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyStateVisibility))]
    public partial GamesEmptyState? EmptyState { get; set; }

    public Visibility EmptyStateVisibility => EmptyState is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Recomputed whenever the list or the library changes.
    /// </summary>
    /// <remarks>
    /// Counted off the collection the page is actually showing rather than worked out from the
    /// filters again, so the message and the emptiness it describes cannot disagree.
    /// </remarks>
    void RefreshEmptyState()
    {
        var visibleCount = 0;
        var collectionGroups = CurrentCollectionView?.CollectionGroups;
        if (collectionGroups is not null)
        {
            foreach (var collectionGroup in collectionGroups)
            {
                if (collectionGroup is ICollectionViewGroup viewGroup)
                {
                    visibleCount += viewGroup.GroupItems.Count;
                }
            }
        }

        var state = GamesEmptyState.For(
            visibleCount,
            GameManager.Instance.GetSynchronisedGamesListCopy().Count,
            GameManager.Instance.SearchText,
            GameManager.Instance.ActiveFilter != GameFilter.All || GameManager.Instance.DllFilter is not null);

        EmptyState = state.Kind == GamesEmptyStateKind.None ? null : state;
    }

    /// <summary>Runs whatever the empty state offered to do.</summary>
    [RelayCommand]
    async Task EmptyStatePrimaryAsync()
    {
        var kind = EmptyState?.Kind;

        if (kind == GamesEmptyStateKind.NoSearchResults)
        {
            gameGridPage.ClearSearchBox();
            return;
        }

        if (kind == GamesEmptyStateKind.FirstRun)
        {
            await RefreshGamesButtonAsync();
            return;
        }

        if (kind == GamesEmptyStateKind.NoUpscalerGames)
        {
            // The button says "Show all 42 games anyway", so it turns off the setting that is
            // hiding them rather than opening the filter dialog and hoping.
            Settings.Instance.HideNonDLSSGames = false;
            ReapplyFilters();
        }
    }

    [RelayCommand]
    async Task EmptyStateSecondaryAsync()
    {
        // Both remaining states offer the same thing: point the app at a folder yourself.
        await AddManualGameButtonAsync();
    }

    public GameGridPageModel(GameGridPage gameGridPage)
    {
        WeakReferenceMessenger.Default.Register<GameLibrariesStateChangedMessage>(this, async (sender, message) =>
        {
            GameManager.Instance.RemoveAllGames();
            await InitialLoadAsync();
        });

        this.gameGridPage = gameGridPage;
        ShowGameCollection();

        // Same reason as the sidebar: games arrive long after this is built, so the counts are
        // taken whenever the library changes rather than once at construction.
        GameManager.Instance.GamesChanged += (sender, args) =>
        {
            UiThread.Run(() =>
            {
                RefreshFilterTabs();

                // Games arrive long after the page is built, so the first-run message has to go
                // when they do rather than sitting over a list that has since filled up.
                RefreshEmptyState();
            });
        };

        RefreshFilterTabs();
    }

    /// <summary>
    /// Reads the cached library, then rescans for what has changed since.
    /// </summary>
    /// <remarks>
    /// Both flags are cleared in a finally. Anything thrown on the way out used to leave them set
    /// for the rest of the session, which is a permanently spinning list and every toolbar button
    /// disabled - the app looking like it is still working with nothing left running.
    /// </remarks>
    public async Task InitialLoadAsync()
    {
        IsGameListLoading = true;
        IsDLSSLoading = true;

        try
        {
            try
            {
                await GameManager.Instance.LoadGamesFromCacheAsync();
            }
            finally
            {
                // Released as soon as the cached list is on screen, rather than after the rescan.
                IsGameListLoading = false;
            }

            await GameManager.Instance.LoadGamesAsync(false);
        }
        finally
        {
            IsDLSSLoading = false;
        }
    }

    public void SearchForGameEvent(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            throw new ArgumentException("Sender must be a TextBox");
        }

        ShowGameCollection(textBox.Text);
    }

    [RelayCommand]
    async Task AddManualGameButtonAsync()
    {
        if (Settings.Instance.DontShowManuallyAddingGamesNotice == false)
        {
            var dontShowAgainCheckbox = new CheckBox()
            {
                Content = new TextBlock()
                {
                    Text = ResourceHelper.GetString("General_DontShowAgain"),
                },
            };

            var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_NoteTitle"),
                PrimaryButtonText = ResourceHelper.GetString("GamesPage_AddGame"),
                SecondaryButtonText = ResourceHelper.GetString("General_ReportIssue"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                Content = new StackPanel()
                {
                    Children = {
                        new TextBlock()
                        {
                            TextWrapping = TextWrapping.Wrap,
                            Text = ResourceHelper.GetString("GamesPage_ManuallyAdding_NoteMessage"),
                        },
                        dontShowAgainCheckbox,
                    },
                    Orientation = Orientation.Vertical,
                    Spacing = 16,
                },
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.None)
            {
                return;
            }


            if (result == ContentDialogResult.Primary)
            {
                // Only dismiss the notice for good once the user has proceeded to add games.
                if (dontShowAgainCheckbox.IsChecked == true)
                {
                    Settings.Instance.DontShowManuallyAddingGamesNotice = true;
                }
                await AddGameManually();
            }
            else if (result == ContentDialogResult.Secondary)
            {
                await Launcher.LaunchUriAsync(new Uri("https://github.com/beeradmoore/dlss-swapper/issues"));
            }
        }
        else
        {
            await AddGameManually();
        }
    }

    async Task AddGameManually()
    {
        TextBlockBuilder textBlockBuilder = new TextBlockBuilder(ResourceHelper.GetString("GamesPage_ManuallyAdding_InfoHtml"));

        if (Settings.Instance.HasShownAddGameFolderMessage == false)
        {
            var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_AnotherNoteTitle"),
                PrimaryButtonText = ResourceHelper.GetString("GamesPage_AddGame"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
                DefaultButton = ContentDialogButton.Primary,
                Content = textBlockBuilder.Build()
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None)
            {
                return;
            }

            Settings.Instance.HasShownAddGameFolderMessage = true;
        }

        var installPath = string.Empty;
        try
        {
            // Associate the HWND with the folder picker
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);


            var folder = FileSystemHelper.OpenFolder(hWnd, okButtonLabel: ResourceHelper.GetString("GamesPage_ManuallyAdding_SelectGameFolder"));

            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            installPath = folder;

            // If top level directory throw error.
            if (installPath == Path.GetPathRoot(installPath))
            {
                var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
                {
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    DefaultButton = ContentDialogButton.Close,
                    Title = ResourceHelper.GetString("General_Error"),
                    Content = ResourceHelper.GetString("GamesPage_ManuallyAdding_TopLevelDirectoryNotSupported"),
                };
                await dialog.ShowAsync();
                return;
            }


            var gameFolderAlreadyExists = GameManager.Instance.CheckIfGameIsAdded(installPath);
            if (gameFolderAlreadyExists == true)
            {
                var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
                {
                    Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_ErrorTitle"),
                    CloseButtonText = ResourceHelper.GetString("General_Close"),
                    Content = ResourceHelper.GetFormattedResourceTemplate("GamesPage_ManuallyAdding_PathExistsTemplate", installPath),
                };
                await dialog.ShowAsync();
                return;
            }

            var manuallyAddGameControl = new ManuallyAddGameControl(installPath);
            var addGameDialog = new FakeContentDialog() //XamlRoot
            {
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                PrimaryButtonText = ResourceHelper.GetString("GamesPage_AddGame"),
                DefaultButton = ContentDialogButton.Primary,
                Content = manuallyAddGameControl,
            };
            addGameDialog.Resources["ContentDialogMinWidth"] = 700;
            addGameDialog.Resources["ContentDialogMaxWidth"] = 700;

            var addGameResult = await addGameDialog.ShowAsync();
            if (manuallyAddGameControl.DataContext is ManuallyAddGameModel manuallyAddGameModel)
            {
                if (addGameResult == ContentDialogResult.Primary)
                {
                    var game = manuallyAddGameModel.Game;
                    await game.SaveToDatabaseAsync();
                    game.ProcessGame();
                    GameManager.Instance.AddGame(game, true);
                }
                else
                {
                    // Cleanup if user is going back.
                    await manuallyAddGameModel.Game.DeleteAsync();
                }
            }
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Attempted to manually add game from path \"{installPath}\" but got an error.");
            var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_ManuallyAdding_ErrorTitle"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
                PrimaryButtonText = ResourceHelper.GetString("General_ReportIssue"),
                DefaultButton = ContentDialogButton.Primary,
                Content = $"{ResourceHelper.GetString("GamesPage_ManuallyAdding_CouldntAddError")}\n\n{ResourceHelper.GetString("General_ErrorMessage")}: {err.Message}",
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri("https://github.com/beeradmoore/dlss-swapper/issues"));
            }
        }
    }

    [RelayCommand]
    async Task RefreshGamesButtonAsync()
    {
        IsDLSSLoading = true;

        await GameManager.Instance.LoadGamesAsync(true);

        IsDLSSLoading = false;

        // A scan can add games and take their first backups, both of which the sidebar counts.
        App.CurrentApp.MainWindow?.RefreshSidebar();
    }

    /// <summary>
    /// Whether the list is broken into a section per launcher.
    /// </summary>
    /// <remarks>
    /// A view option, so it lives in the View menu beside grid and list rather than behind a
    /// "Filter" button, which is where it used to be and which is not what it does: it changes how
    /// the same games are arranged, never which ones are shown.
    ///
    /// It applies on click, like view type already does. Behind the old dialog it needed an Apply.
    /// </remarks>
    public bool IsGroupedByLibrary
    {
        get => Settings.Instance.GroupGameLibrariesTogether;
        set
        {
            if (Settings.Instance.GroupGameLibrariesTogether == value)
            {
                return;
            }

            Settings.Instance.GroupGameLibrariesTogether = value;
            OnPropertyChanged();

            // Through the collection rather than by swapping the source: the grouped branch is what
            // re-assigns the per-library filters, and nothing else refreshes them.
            CurrentCollectionView = null;
            ReapplyFilters();
        }
    }

    [RelayCommand]
    void ToggleGroupByLibrary()
    {
        IsGroupedByLibrary = !IsGroupedByLibrary;
    }

    /// <summary>
    /// Runs whatever the row's button offers, which depends on what the row is saying.
    /// </summary>
    /// <remarks>
    /// One command rather than one per state, because the button's meaning comes from the row's
    /// status and the two must not be able to disagree. They did: the button was wired to the
    /// update command whatever it said, so "Save a copy" ran an update.
    /// </remarks>
    [RelayCommand]
    async Task RowActionAsync(Game? game)
    {
        if (game is null)
        {
            return;
        }

        var status = GameRowStatus.For(game);

        if (status.State == GameRowState.HasUpdates)
        {
            await UpdateGameAsync(game);
            return;
        }

        if (status.State == GameRowState.NoBackup)
        {
            var saved = await game.SaveOriginalCopiesAsync();

            if (saved == 0)
            {
                var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
                {
                    Title = ResourceHelper.GetString("GamesPage_Action_SaveACopy"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    Content = ResourceHelper.GetFormattedResourceTemplate("GamesPage_SaveACopyFailedTemplate", game.Title),
                };
                await dialog.ShowAsync();
            }
        }

        // Both branches change the backup coverage the sidebar reports.
        App.CurrentApp.MainWindow?.RefreshSidebar();
    }

    /// <summary>
    /// Marks a game as one that bulk updates should leave alone, or unmarks it.
    /// </summary>
    /// <remarks>
    /// For games where a newer dll causes a problem rather than fixes one, most often anti cheat in
    /// a multiplayer title refusing to launch with a modified dll. Without this the only way to keep
    /// such a game safe is to never use update all, which gives up the feature for the whole
    /// library to protect one game.
    /// </remarks>
    [RelayCommand]
    async Task ToggleSkipUpdatesAsync(Game? game)
    {
        if (game is null)
        {
            return;
        }

        game.SkipUpdates = game.SkipUpdates == false;
        await game.SaveToDatabaseAsync();

        // GameFilters.HasUpdate excludes a game told to skip updates, so the row leaves the "Have
        // an update" list the moment this is set - but nothing recounted, so the tab kept the old
        // number and Review opened a sheet with one fewer row than the button had counted.
        GameManager.Instance.NotifyGamesChanged();
    }

    /// <summary>
    /// Updates every out of date dll in one game, from its row.
    /// </summary>
    /// <remarks>
    /// No sheet for a single row: the row already named the game and the button already named the
    /// action, so a sheet would only ask the question the click just answered. It still runs
    /// through the same batch and ends on the same strip, so it is as undoable as any other.
    /// </remarks>
    [RelayCommand]
    async Task UpdateGameAsync(Game? game)
    {
        if (game is null)
        {
            return;
        }

        await RunUpdateBatchAsync(PendingDllUpdate.ForGames(new List<Game>() { game }));
    }

    /// <summary>The preview sheet's contents, or null when it is closed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdatePreviewVisibility))]
    public partial UpdatePreviewModel? UpdatePreview { get; set; }

    public Visibility UpdatePreviewVisibility => UpdatePreview is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Looks for a cover for every game on the page at once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="GamesOnThePage"/> like everything else that acts on "the games", so with a
    /// filter on it means the games shown - the same rule Update all games follows, and for the
    /// same reason.
    /// </para>
    /// <para>
    /// The scan proposes only what it is certain of and names the rest; see
    /// <see cref="CoverScanModel"/> for why that is the only safe shape for doing this in bulk.
    /// </para>
    /// </remarks>
    [RelayCommand]
    async Task FindCoversAsync()
    {
        // Before the key prompt, not after it. An empty list used to be checked second, so pressing
        // this with a search that matches nothing walked somebody through making an api key and
        // then did nothing at all, with no line of text either way.
        var games = GamesOnThePage();
        if (games.Count == 0)
        {
            var nothingToScan = new EasyContentDialog(gameGridPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("CoverScan_Title"),
                Content = ResourceHelper.GetString("CoverScan_NothingToScan"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
            };

            _ = await nothingToScan.ShowAsync();

            return;
        }

        // Somebody without a key gets the steps, a link to the page that makes one, and a box to
        // paste it into - and then carries straight on into the scan they asked for, rather than
        // being sent to Settings and back to press this button a second time. Somebody who already
        // has a key sees none of it.
        if (await SteamGridDbKeyPrompt.EnsureKeyAsync(gameGridPage.XamlRoot, ResourceHelper.GetString("CoverScan_Title")) == false)
        {
            return;
        }

        var scan = new CoverScanDialog(games);

        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("CoverScan_Title"),
            Content = scan,
            CloseButtonText = ResourceHelper.GetString("General_Close"),
        };

        // Same caps as the per-game picker, and for the same reason: a ContentDialog is 548x756
        // whatever its content asks for, and it clips rather than scrolls.
        dialog.Resources["ContentDialogMaxWidth"] = 760d;
        dialog.Resources["ContentDialogMaxHeight"] = 960d;

        _ = await dialog.ShowAsync();

        // Stops anything still running and drops the undo copies, which nothing can reach once the
        // dialog carrying the undo button has gone.
        scan.ViewModel.Close();
    }

    /// <summary>
    /// Opens the preview sheet rather than starting to write.
    /// </summary>
    /// <remarks>
    /// The button says "Review", and this is what makes that true. Replacing files inside a game
    /// install is the one thing this app does that looks irreversible, and it used to happen behind
    /// a confirmation that only gave a count.
    /// </remarks>
    [RelayCommand]
    void UpdateAllGamesButton()
    {
        // The same games the button counted, through the same predicate that counted them. Not
        // merely the same list: the button's number is the count of games matching HasUpdate, so
        // anything that rule excludes — a hidden game, one marked leave alone — has to be excluded
        // here too or the sheet opens holding rows the button never counted.
        var hideNonDLSSGames = Settings.Instance.HideNonDLSSGames;
        var behind = GamesOnThePage()
            .Where(x => GameFilters.Matches(x, GameFilter.HasUpdate, hideNonDLSSGames))
            .ToList();

        var pendingUpdates = PendingDllUpdate.ForGames(behind);
        if (pendingUpdates.Count == 0)
        {
            return;
        }

        UpdatePreview = new UpdatePreviewModel(pendingUpdates);
    }

    /// <summary>
    /// Opens the preview sheet for one game, from that game's own page.
    /// </summary>
    /// <remarks>
    /// So the game page gets the same review-then-write-then-undo the games page has, rather than
    /// the modal progress and summary dialogs it used to run. Everything after this point is
    /// shared: the sheet lists exactly what will be written, and the strip that follows keeps what
    /// it wrote so it can put that batch back.
    /// </remarks>
    public void ShowUpdatePreviewFor(Game game)
    {
        var pendingUpdates = PendingDllUpdate.ForGames(new[] { game });
        if (pendingUpdates.Count == 0)
        {
            return;
        }

        UpdatePreview = new UpdatePreviewModel(pendingUpdates);
    }

    /// <summary>Dismisses the sheet without writing anything.</summary>
    [RelayCommand]
    void CancelUpdatePreview()
    {
        UpdatePreview = null;
    }

    [RelayCommand]
    async Task ConfirmUpdatePreviewAsync()
    {
        var selectedUpdates = UpdatePreview?.SelectedUpdates;
        UpdatePreview = null;

        if (selectedUpdates is not null)
        {
            // The sheet's own rows, so what runs is what was approved rather than everything that
            // happened to be out of date when the run started.
            await RunUpdateBatchAsync(selectedUpdates);
        }
    }

    /// <summary>The strip along the bottom, or null when there is nothing to report.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateBatchVisibility))]
    [NotifyPropertyChangedFor(nameof(ContentBottomMargin))]
    public partial UpdateBatchModel? UpdateBatch { get; set; }

    public Visibility UpdateBatchVisibility => UpdateBatch is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Keeps the list clear of the batch strip while it is showing.
    /// </summary>
    /// <remarks>
    /// 52px, which is the strip's height. The strip is docked over the content rather than taking a
    /// row of its own, so the bottom of the list has to give the same space back or the last game
    /// sits behind it, and a list that ends behind an opaque bar looks like a list that ended.
    /// </remarks>
    public Thickness ContentBottomMargin => UpdateBatch is null
        ? new Thickness(0)
        : new Thickness(0, 0, 0, 52);

    CancellationTokenSource? batchCancellation;

    /// <summary>
    /// Writes a batch, with the progress and the outcome shown in the page rather than in a dialog.
    /// </summary>
    /// <remarks>
    /// A modal progress dialog blocked the library while the app wrote to it, so the one thing the
    /// user might want to look at during a long run - which games are done - was the one thing
    /// covered up. The strip leaves the rows visible and updating.
    /// </remarks>
    async Task RunUpdateBatchAsync(IReadOnlyList<PendingDllUpdate> updates)
    {
        if (updates.Count == 0)
        {
            return;
        }

        var batch = new UpdateBatchModel();
        UpdateBatch = batch;

        using var cancellation = new CancellationTokenSource();
        batchCancellation = cancellation;

        DllUpdateResult result;
        try
        {
            result = await DllUpdateRunner.UpdateSelectedAsync(updates, new Progress<DllUpdateProgress>(batch.Report), cancellation.Token);
        }
        finally
        {
            batchCancellation = null;
        }

        batch.Complete(result);

        // Swapping saves an original first, so the backup coverage moves with it.
        App.CurrentApp.MainWindow?.RefreshSidebar();

        // And so does how many games are behind. The swap path refreshes each game on its own but
        // raises nothing, so the tab count and the accent button kept the number they had before
        // the batch - leaving "Review 7 updates" on screen over rows that all read "Up to date",
        // which then recomputed to nothing and returned without doing anything when pressed.
        GameManager.Instance.NotifyGamesChanged();
    }

    /// <summary>
    /// Stops after the file being written, rather than part way through one.
    /// </summary>
    /// <remarks>
    /// The label said so before it was pressed, so the button has to keep that promise: the token
    /// is checked between files, never during.
    /// </remarks>
    [RelayCommand]
    void StopUpdateBatch()
    {
        if (UpdateBatch is null)
        {
            return;
        }

        UpdateBatch.CanStop = false;
        UpdateBatch.StopLabel = ResourceHelper.GetString("Update_Stopping");
        batchCancellation?.Cancel();
    }

    [RelayCommand]
    void DismissUpdateBatch()
    {
        UpdateBatch = null;
    }

    /// <summary>
    /// Puts back everything the last batch wrote.
    /// </summary>
    /// <remarks>
    /// The reason the whole flow can be offered without a warning dialog: the batch is reversible,
    /// and the strip that says so is on screen at the moment it matters.
    /// </remarks>
    [RelayCommand]
    async Task UndoUpdateBatchAsync()
    {
        var batch = UpdateBatch;
        if (batch is null || batch.CanUndo == false)
        {
            return;
        }

        var writtenItems = batch.WrittenItems;

        batch.CanUndo = false;
        batch.IsDone = false;
        batch.CanStop = false;
        batch.ProgressText = ResourceHelper.GetString("Update_Undoing");
        batch.CurrentItemText = string.Empty;

        using var cancellation = new CancellationTokenSource();
        batchCancellation = cancellation;

        DllUpdateResult result;
        try
        {
            result = await DllUpdateRunner.UndoAsync(writtenItems, new Progress<DllUpdateProgress>(batch.Report), cancellation.Token);
        }
        finally
        {
            batchCancellation = null;
        }

        batch.CompleteUndo(result);
        App.CurrentApp.MainWindow?.RefreshSidebar();

        // Putting the batch back makes those games behind again, which is the same recount.
        GameManager.Instance.NotifyGamesChanged();
    }

    /// <summary>
    /// Names what each replaced file was, and what it is now.
    /// </summary>
    /// <remarks>
    /// The done strip can only say how many files were written. The version each one came from is
    /// the thing worth checking before deciding to keep the batch, and it is knowable only while
    /// the run is happening, so it is recorded then and read here.
    /// </remarks>
    [RelayCommand]
    async Task ShowBatchChangesAsync()
    {
        var batch = UpdateBatch;
        if (batch is null || batch.HasChanges == false)
        {
            return;
        }

        var rows = new StackPanel() { Spacing = 10 };

        foreach (var change in batch.Changes)
        {
            var row = new StackPanel() { Spacing = 2 };

            row.Children.Add(new TextBlock()
            {
                Text = change.Description,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            });

            // The versions on their own line rather than appended to the title, because the title
            // is the part that varies in length and would push the change off the end.
            row.Children.Add(new TextBlock()
            {
                Text = change.VersionChange,
                FontSize = 12,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DsTextSecondaryBrush"],
            });

            rows.Children.Add(row);
        }

        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("Update_SeeWhatChanged"),
            CloseButtonText = ResourceHelper.GetString("General_Okay"),
            Content = new ScrollViewer()
            {
                MaxHeight = 400,
                Content = rows,
            },
        };

        await dialog.ShowAsync();
    }

    /// <summary>
    /// Names the files that could not be replaced.
    /// </summary>
    /// <remarks>
    /// Listed rather than counted, because "2 could not be replaced" does not tell you which game
    /// to close or which needs running as administrator.
    /// </remarks>
    [RelayCommand]
    async Task ShowBatchFailuresAsync()
    {
        var batch = UpdateBatch;
        if (batch is null || batch.HasFailures == false)
        {
            return;
        }

        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("DllUpdate_FailuresHeader"),
            CloseButtonText = ResourceHelper.GetString("General_Okay"),
            Content = new ScrollViewer()
            {
                MaxHeight = 400,
                Content = new TextBlock()
                {
                    Text = string.Join(Environment.NewLine, batch.Failures),
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        await dialog.ShowAsync();
    }

    [RelayCommand]
    async Task UnknownAssetsFoundButtonAsync()
    {
        var newDllsControl = new NewDLLsControl();

        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("GamesPage_NewDllsFound"),
            CloseButtonText = ResourceHelper.GetString("General_Close"),
            Content = newDllsControl,
        };
        dialog.Resources["ContentDialogMinWidth"] = 700;
        dialog.Resources["ContentDialogMaxWidth"] = 700;
        await dialog.ShowAsync();
    }

    [RelayCommand]
    void ChangeGameGridView(GameGridViewType gameGridView)
    {
        if (gameGridView == this.GameGridViewType)
        {
            return;
        }

        GameGridViewType = gameGridView;
        gameGridPage.ReloadMainContentControl();
        Settings.Instance.GameGridViewType = gameGridView;
    }
}
