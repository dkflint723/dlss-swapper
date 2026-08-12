using DLSS_Swapper.Data;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
using Windows.System;
using AsyncAwaitBestPractices;
using CommunityToolkit.WinUI;
using System.Threading;
using DLSS_Swapper.Helpers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DLSS_Swapper.Pages;


/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GameGridPage : Page
{
    public static string PageTag { get; } = "PageTag_Games";

    /*
    public List<IGameLibrary> GameLibraries { get; } = new List<IGameLibrary>();

    Dictionary<GameLibrary, ObservableCollection<Game>> allGames = new Dictionary<GameLibrary, ObservableCollection<Game>>();


    public List<GameGroup> GroupedGameGroups { get; } = new List<GameGroup>();
    public List<GameGroup> UngroupedGameGroups { get; } = new List<GameGroup>();

    ObservableCollection<Game> FavouriteGames = new ObservableCollection<Game>();
    ObservableCollection<Game> AllGames = new ObservableCollection<Game>();
    */

    bool _loadingGamesAndDlls;
    Timer? _saveScrollSizeTimer;

    public GameGridPageModel ViewModel { get; private set; }

    public GameGridPage()
    {
        this.InitializeComponent();
        ViewModel = new GameGridPageModel(this);
        DataContext = ViewModel;
    }

    bool hasFirstLoaded;
    void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (hasFirstLoaded)
        {
            return;
        }
        hasFirstLoaded = true;

        if (DataContext is GameGridPageModel gameGridPageModel)
        {
            gameGridPageModel.InitialLoadAsync().SafeFireAndForget((err) =>
            {
                Logger.Error(err, $"Unable to perform initial load");
            });
        }

        //await LoadGamesAndDlls();
        //await LoadGamesFromCacheAsync();
        //UpdateGameLibraries();
        //await LoadGames();
    }


    async Task LoadGamesAndDlls()
    {
        // TODO: REMOVE
        await Task.Delay(1);

        if (_loadingGamesAndDlls)
            return;

        _loadingGamesAndDlls = true;

        // TODO: Fade?
        //LoadingStackPanel.Visibility = Visibility.Visible;

        /*
        var tasks = new List<Task>();
        tasks.Add(LoadGamesAsync());


        await Task.WhenAll(tasks);

        */
        App.CurrentApp.RunOnUIThread(() =>
        {
            //LoadingStackPanel.Visibility = Visibility.Collapsed;
            _loadingGamesAndDlls = false;
        });
    }

    internal void ScrollToGame(Game game)
    {
        if (MainContentControl.ContentTemplateRoot is GridView mainGridView)
        {
            App.CurrentApp.RunOnUIThreadAsync(async () =>
            {
                var indexOfGame = mainGridView.Items.IndexOf(game);
                if (indexOfGame >= 0)
                {
                    await mainGridView.SmoothScrollIntoViewWithItemAsync(indexOfGame);
                }
            }).SafeFireAndForget();
        }
        else if (MainContentControl.ContentTemplateRoot is ListView mainListView)
        {
            App.CurrentApp.RunOnUIThreadAsync(async () =>
            {
                var indexOfGame = mainListView.Items.IndexOf(game);
                if (indexOfGame >= 0)
                {
                    await mainListView.SmoothScrollIntoViewWithItemAsync(indexOfGame);
                }
            }).SafeFireAndForget();
        }
    }

    internal void ReloadMainContentControl()
    {
        MainContentControl.Content = null;
        MainContentControl.Content = ViewModel;
    }

    // This fires for both the GridView and the ListView
    void GridAndListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Game selectedGame)
        {
            if (selectedGame.Processing)
            {
                var dialog = new EasyContentDialog(XamlRoot)
                {
                    Title = ResourceHelper.GetString("Game_CurrentlyProcessing"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    Content = ResourceHelper.GetFormattedResourceTemplate("GamePage_ProcessingPleaseWaitTemplate", selectedGame.Title),
                };
                _ = dialog.ShowAsync();
                return;
            }

            // A page now, not a dialog over this one. This page stays cached underneath, so coming
            // back lands on the same scroll position and the same tab.
            App.CurrentApp.MainWindow?.ShowGame(selectedGame);
        }
    }


    void MainGridView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;

            if (sender is GridView gridView)
            {
                double scaleAmount = delta > 0 ? 1.05 : 0.95;
                var newWidth = (int)(ViewModel.GridViewItemWidth * scaleAmount);

                if (newWidth > 60 && newWidth < 600)
                {
                    ViewModel.GridViewItemWidth = newWidth;

                    if (_saveScrollSizeTimer is not null)
                    {
                        _saveScrollSizeTimer.Dispose();
                        _saveScrollSizeTimer = null;
                    }

                    _saveScrollSizeTimer = new Timer((state) =>
                    {
                        Settings.Instance.GridViewItemWidth = ViewModel.GridViewItemWidth;
                    }, null, 500, Timeout.Infinite);
                }
            }

            e.Handled = true;
        }
    }

    private void ClearSearchBox_Click(object sender, RoutedEventArgs e)
    {
        ClearSearchBox();
    }

    /// <summary>
    /// Empties the search box, which is what actually re-runs the search.
    /// </summary>
    /// <remarks>
    /// The box owns the text, so the empty state's "Clear search" has to go through it rather than
    /// clearing the collection behind its back and leaving the query still sitting there.
    /// </remarks>
    internal void ClearSearchBox()
    {
        SearchBox.Text = string.Empty;
    }

    /// <summary>
    /// Clicking the dimmed area behind the preview sheet cancels it.
    /// </summary>
    /// <remarks>
    /// One of three ways out, all of them the same command, because a sheet that writes to game
    /// folders must never be harder to escape than to accept.
    /// </remarks>
    void UpdatePreviewBackdrop_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.CancelUpdatePreviewCommand.Execute(null);
    }

    void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.UpdatePreview is null)
        {
            return;
        }

        ViewModel.CancelUpdatePreviewCommand.Execute(null);

        // Only swallowed when it did something, so Esc stays free for everything else on the page.
        args.Handled = true;
    }

    /// <summary>
    /// Folds or unfolds a launcher section, and keeps its heading where it was.
    /// </summary>
    /// <remarks>
    /// Folding removes the games from the view rather than hiding them, because a GridView sizes
    /// every cell from the first item it measures and hiding rows collapsed the whole grid. The cost
    /// is that the content gets shorter, so the scroll viewer clamps how far down it is; unfolding
    /// puts the games back above where it now sits, and they arrive off the top of the screen.
    ///
    /// So the heading is brought back into view afterwards. No alignment ratio, which means the
    /// least scrolling that works: a heading still on screen does not move at all, and one that has
    /// gone off the top comes back just far enough to see. Queued rather than called here, because
    /// at this point the list has not been laid out again and the heading's new position does not
    /// exist yet.
    /// </remarks>
    void GroupHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: GameGroup group } header)
        {
            group.ToggleExpanded();

            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                header.StartBringIntoView(new BringIntoViewOptions() { AnimationDesired = false });
            });
        }
    }
}
