using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Builders;
using DLSS_Swapper.Data;
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
    [NotifyPropertyChangedFor(nameof(GridViewItemHeight))]
    public partial int GridViewItemWidth { get; set; } = Settings.Instance.GridViewItemWidth;

    public int GridViewItemHeight => (int)(GridViewItemWidth * 1.5);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameGridViewIcon))]
    public partial GameGridViewType GameGridViewType { get; set; } = Settings.Instance.GameGridViewType;

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
    public void RefreshFilterTabs()
    {
        var games = GameManager.Instance.GetSynchronisedGamesListCopy();
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
        ReviewUpdatesText = ResourceHelper.GetFormattedResourceTemplate("GamesPage_ReviewUpdatesTemplate", behind);
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
        CurrentCollectionView = GameManager.Instance.GetGameCollection();
        RefreshFilterTabs();
    }

    /// <summary>Switches the page to a filter tab. Also used by the sidebar's backup card.</summary>
    public void ShowFilter(GameFilter filter)
    {
        GameManager.Instance.ActiveFilter = filter;
        CurrentCollectionView = GameManager.Instance.GetGameCollection();
        RefreshFilterTabs();
    }

    public GameGridPageModel(GameGridPage gameGridPage)
    {
        WeakReferenceMessenger.Default.Register<GameLibrariesStateChangedMessage>(this, async (sender, message) =>
        {
            GameManager.Instance.RemoveAllGames();
            await InitialLoadAsync();
        });

        this.gameGridPage = gameGridPage;
        ApplyGameGroupFilter();

        // Same reason as the sidebar: games arrive long after this is built, so the counts are
        // taken whenever the library changes rather than once at construction.
        GameManager.Instance.GamesChanged += (sender, args) =>
        {
            UiThread.Run(RefreshFilterTabs);
        };

        RefreshFilterTabs();
    }

    public async Task InitialLoadAsync()
    {
        IsGameListLoading = true;
        IsDLSSLoading = true;

        await GameManager.Instance.LoadGamesFromCacheAsync();

        IsGameListLoading = false;

        await GameManager.Instance.LoadGamesAsync(false);

        IsDLSSLoading = false;
    }

    public void SearchForGameEvent(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            throw new ArgumentException("Sender must be a TextBox");
        }

        if (string.IsNullOrEmpty(textBox.Text))
        {
            CurrentCollectionView = GameManager.Instance.GetGameCollection();
            return;
        }
        CurrentCollectionView = GameManager.Instance.GetGameCollection(textBox.Text);
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

    [RelayCommand]
    async Task FilterGamesButtonAsync()
    {
        var gameFilterControl = new GameFilterControl();

        var dialog = new EasyContentDialog(gameGridPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("General_Filter"),
            PrimaryButtonText = ResourceHelper.GetString("General_Apply"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = gameFilterControl,
        };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (gameFilterControl.DataContext is GameFilterControlViewModel gameFilterControlViewModel)
            {
                GameManager.Instance.ShowHiddenGames = gameFilterControlViewModel.ShowHiddenGames;
                Settings.Instance.GroupGameLibrariesTogether = gameFilterControlViewModel.GroupGameLibrariesTogether;
            }

            ApplyGameGroupFilter();
        }

    }

    void ApplyGameGroupFilter()
    {
        // TODO: Remove weird hack which otherwise causes MainGridView_SelectionChanged to fire when changing MainGridView.ItemsSource.
        //gameGridPage.MainGridView.SelectionChanged -= MainGridView_SelectionChanged;

        //MainGridView.ItemsSource = null;
        CurrentCollectionView = null;
        CurrentCollectionView = GameManager.Instance.GetGameCollection();
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
    }

    /// <summary>
    /// Updates every out of date dll in one game, from its card.
    /// </summary>
    /// <remarks>
    /// Runs through the same prompt and the same runner as updating every game, so there is one
    /// swap path rather than a second shorter one that skips the confirmation or the backup.
    /// </remarks>
    [RelayCommand]
    async Task UpdateGameAsync(Game? game)
    {
        if (game is null)
        {
            return;
        }

        var outdatedDllCount = game.OutdatedAssetTypes.Count;

        await DllUpdatePrompt.RunAsync(
            gameGridPage.XamlRoot,
            new List<Game>() { game },
            ResourceHelper.GetString("DllUpdate_Title"),
            outdatedDllCount,
            ResourceHelper.GetFormattedResourceTemplate("DllUpdate_ConfirmOneGameTemplate", outdatedDllCount, game.Title),
            ResourceHelper.GetString("DllUpdate_AllGamesUpToDate"),
            (games, progress, cancellationToken) => DllUpdateRunner.UpdateGamesAsync(games, progress, cancellationToken),
            "DllUpdate_SwappedTemplate");

        // Swapping saves an original first, so the backup coverage moves with it.
        App.CurrentApp.MainWindow?.RefreshSidebar();
    }

    [RelayCommand]
    async Task UpdateAllGamesButtonAsync()
    {
        var gamesToUpdate = new List<Game>();
        var outdatedDllCount = 0;

        foreach (var game in GameManager.Instance.GetSynchronisedGamesListCopy())
        {
            if (game.OutdatedAssetTypes.Count == 0)
            {
                continue;
            }

            gamesToUpdate.Add(game);
            outdatedDllCount += game.OutdatedAssetTypes.Count;
        }

        await DllUpdatePrompt.RunAsync(
            gameGridPage.XamlRoot,
            gamesToUpdate,
            ResourceHelper.GetString("DllUpdate_Title"),
            outdatedDllCount,
            ResourceHelper.GetFormattedResourceTemplate("DllUpdate_ConfirmAllGamesTemplate", outdatedDllCount, gamesToUpdate.Count),
            ResourceHelper.GetString("DllUpdate_AllGamesUpToDate"),
            (games, progress, cancellationToken) => DllUpdateRunner.UpdateGamesAsync(games, progress, cancellationToken),
            "DllUpdate_SwappedTemplate");

        // Swapping saves an original first, so the backup coverage moves with it.
        App.CurrentApp.MainWindow?.RefreshSidebar();
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
