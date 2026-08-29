using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Pages;
using DLSS_Swapper.UserControls;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using Windows.System;

namespace DLSS_Swapper;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindowModel ViewModel { get; private set; }

    IntPtr _windowIcon;

    readonly WindowPositionRect _trackedWindow = new WindowPositionRect();

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, string iconPath, ref IntPtr index);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int DestroyIcon(IntPtr hIcon);

    public MainWindow()
    {
        this.InitializeComponent();
        ViewModel = new MainWindowModel();

        if (AppWindow?.Presenter is OverlappedPresenter overlappedPresenter)
        {
            var lastWindowSizeAndPosition = Settings.Instance.LastWindowSizeAndPosition;
            _trackedWindow = new WindowPositionRect(lastWindowSizeAndPosition);

            if (lastWindowSizeAndPosition.Width > 512 && lastWindowSizeAndPosition.Height > 512)
            {
                AppWindow.MoveAndResize(lastWindowSizeAndPosition.GetRectInt32());
            }
            if (lastWindowSizeAndPosition.State == WindowState.Maximized)
            {
                overlappedPresenter.Maximize();
            }
        }

        AppWindow?.Changed += (AppWindow sender, AppWindowChangedEventArgs args) =>
        {
            if (args.DidPositionChange)
            {
                if (sender.Presenter is OverlappedPresenter presenter)
                {
                    var isCurrentlyMinimizedOrMaximized =
                        presenter.State == OverlappedPresenterState.Minimized ||
                        presenter.State == OverlappedPresenterState.Maximized;

                    if (isCurrentlyMinimizedOrMaximized == false)
                    {
                        _trackedWindow.UpdatePosition(sender.Position);
                    }
                }
            }
        };

        SizeChanged += (object sender, WindowSizeChangedEventArgs args) =>
        {
            if (AppWindow?.Presenter is OverlappedPresenter overlappedPresenter)
            {
                var currentState = overlappedPresenter.State;
                var isTransitioningToMaximized =
                    currentState == OverlappedPresenterState.Maximized &&
                    _trackedWindow.State != WindowState.Maximized;

                if (isTransitioningToMaximized == false && currentState != OverlappedPresenterState.Maximized)
                {
                    _trackedWindow.UpdateFromAppWindow(AppWindow);
                }

                _trackedWindow.State = (WindowState)overlappedPresenter.State;
            }
        };

        Closed += (object sender, WindowEventArgs args) =>
        {
            if (AppWindow?.Presenter is OverlappedPresenter overlappedPresenter)
            {
                Settings.Instance.LastWindowSizeAndPosition = new WindowPositionRect(_trackedWindow);
            }

            // Release the icon.
            if (_windowIcon != IntPtr.Zero)
            {
                DestroyIcon(_windowIcon);
                _windowIcon = IntPtr.Zero;
            }
        };

        if (WindowManager.IsCustomizationSupported)
        {
            var appWindow = App.CurrentApp.WindowManager.GetAppWindowForWindow(this);
            var appWindowTitleBar = appWindow.TitleBar;
            appWindowTitleBar.ExtendsContentIntoTitleBar = true;
            RootGrid.RowDefinitions[0].Height = new GridLength(32);
        }
        else
        {
            RootGrid.RowDefinitions[0].Height = new GridLength(28);
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }

        SetIcon();

        // The sidebar's labels come from its own translation properties, which repaint themselves
        // on a language change, so nothing needs poking here any more. The counts do not, since
        // they are formatted strings rather than plain lookups.
        LanguageManager.Instance.OnLanguageChanged += () =>
        {
            Sidebar?.ViewModel.Refresh();
        };
    }



    /// <summary>
    /// Default the Window Icon to the icon stored in the .exe, if any.
    ///
    /// The Icon can be overriden by callers by calling SetIcon themselves.
    /// </summary>
    /// via this MAUI PR https://github.com/dotnet/maui/pull/6900
    void SetIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var index = IntPtr.Zero; // 0 = first icon in resources
            _windowIcon = ExtractAssociatedIcon(IntPtr.Zero, processPath, ref index);
            if (_windowIcon != IntPtr.Zero)
            {
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

                var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(windowHandle));
                if (appWindow is not null)
                {
                    var iconId = Win32Interop.GetIconIdFromIcon(_windowIcon);
                    appWindow.SetIcon(iconId);
                }
            }
        }
    }



    /// <summary>
    /// Recomputes the sidebar's counts and the backup card.
    /// </summary>
    /// <remarks>
    /// They are a snapshot, like every other count in this app, so anything that changes the games
    /// or their backups has to say so. Called after the library loads, after a scan, and after any
    /// swap or saved copy.
    /// </remarks>
    internal void RefreshSidebar()
    {
        Sidebar?.ViewModel.Refresh();
    }

    void Sidebar_SectionInvoked(object? sender, ShellSection section)
    {
        GoToPage(PageTagForSection(section));
    }

    /// <summary>
    /// Leaves the upscalers page for the games using one dll.
    /// </summary>
    /// <remarks>
    /// Navigating first, so the page exists to be filtered — the same order the backup card uses,
    /// and for the same reason: on a cold start the games page has not been built yet.
    /// </remarks>
    public void ShowGamesUsingDll(DllFilter dllFilter)
    {
        GoToPage(GameGridPage.PageTag);
        gameGridPage?.ViewModel.ShowGamesUsingDll(dllFilter);
    }

    /// <summary>
    /// Opens one game's page.
    /// </summary>
    /// <remarks>
    /// Built fresh every time and deliberately not cached: the page is about one game, and a cached
    /// instance would be about whichever game was opened first. The games page it came from *is*
    /// cached, so going back lands where you left.
    /// </remarks>
    public void ShowGame(Game game)
    {
        // The games page first, then the sheet over it. This used to replace the whole frame, which
        // meant coming back rebuilt the list and lost where you were in it - the scroll position,
        // the search, the tab. Nothing navigates now, so there is nothing to restore.
        GoToPage(GameGridPage.PageTag);

        // The sidebar stays on Games. A game is something you open from that list and close again,
        // not a fourth section of the app.
        Sidebar.ViewModel.ActiveSection = ShellSection.Games;

        gameGridPage?.ShowGameDetail(game);
    }

    /// <summary>Back to the games list, from a game's own sheet.</summary>
    /// <remarks>
    /// Still called GoToGames because that is what it means to the caller, and GameControlModel's
    /// close command has always said it that way. It closes the sheet rather than navigating.
    /// </remarks>
    public void GoToGames()
    {
        // GoToPage closes the sheet, so there is nothing else to do here.
        GoToPage(GameGridPage.PageTag);
    }

    void Sidebar_FixMissingBackupsInvoked(object? sender, EventArgs e)
    {
        // Lands on the games missing a copy, which is what the card offers. Navigating first, so
        // the page exists to be filtered.
        GoToPage(GameGridPage.PageTag);
        gameGridPage?.ViewModel.ShowFilter(GameFilter.MissingBackup);
    }

    static string PageTagForSection(ShellSection section)
    {
        return section switch
        {
            ShellSection.Upscalers => LibraryPage.PageTag,
            ShellSection.Settings => SettingsPage.PageTag,
            _ => GameGridPage.PageTag,
        };
    }

    static ShellSection? SectionForPageTag(string page)
    {
        if (page == GameGridPage.PageTag) { return ShellSection.Games; }
        if (page == LibraryPage.PageTag) { return ShellSection.Upscalers; }
        if (page == SettingsPage.PageTag) { return ShellSection.Settings; }

        // Acknowledgements has no sidebar item, so nothing should look selected.
        return null;
    }


    GameGridPage? gameGridPage;
    LibraryPage? libraryPage;
    SettingsPage? settingsPage;

    public GameGridPage? GameGridPage => gameGridPage;

    void GoToPage(string page)
    {
        ViewModel.AcknowledgementsVisibility = Visibility.Collapsed;

        if (page == GameGridPage.PageTag)
        {
            if (ContentFrame.Content is null || ContentFrame.Content as Page != gameGridPage)
            {
                ContentFrame.Content = gameGridPage ??= new GameGridPage();
            }

            // Every arrival at the games page closes the sheet, here rather than at the callers.
            // The sheet lives on this page and the page is cached, so leaving and coming back used
            // to bring the sheet with it: "show games using this dll" and the sidebar's missing
            // backup card both landed on a filtered list hidden under a game opened minutes
            // earlier, and clicking Games in the sidebar to escape did nothing at all, because the
            // branch above sees the page is already there and stops. Navigating away from a detail
            // view used to be impossible to get wrong - the detail WAS the page.
            gameGridPage?.CloseGameDetail();
        }
        else if (page == LibraryPage.PageTag)
        {
            if (ContentFrame.Content is null || ContentFrame.Content as Page != libraryPage)
            {
                ContentFrame.Content = libraryPage ??= new LibraryPage();
            }
        }
        else if (page == SettingsPage.PageTag)
        {
            if (ContentFrame.Content is null || ContentFrame.Content as Page != settingsPage)
            {
                ContentFrame.Content = settingsPage ??= new SettingsPage();
            }
        }
        else if (page ==  AcknowledgementsPage.PageTag)
        {
            if (ContentFrame.Content is null || ContentFrame.Content is not AcknowledgementsPage)
            {
                ViewModel.AcknowledgementsVisibility = Visibility.Visible;
                ContentFrame.Content = new AcknowledgementsPage();
            }
        }
        else
        {
            Logger.Error($"Attempting to navigate to a page that was not found, {page}");
            return;
        }

        // Navigation can come from somewhere other than the sidebar, so the active marker is set
        // from the destination rather than from the click.
        var section = SectionForPageTag(page);
        if (section is not null)
        {
            Sidebar.ViewModel.ActiveSection = section.Value;
        }
    }

    internal void GoToAcknowledgements()
    {
        GoToPage(AcknowledgementsPage.PageTag);
    }

    async void ShellGrid_Loaded(object sender, RoutedEventArgs e)
    {


        // TODO: Disabled because CommunityToolkit.WinUI.Helpers.SystemInformation.Instance.IsAppUpdated throws exceptions for unpackaged apps.
        /*
        // If this is a new build, fetch updates to display to the user.
        Task<Data.GitHub.GitHubRelease> releaseNotesTask = null;
        if (CommunityToolkit.WinUI.Helpers.SystemInformation.Instance.IsAppUpdated)
        {
            var currentAppVersion = App.CurrentApp.GetVersion();
            releaseNotesTask = gitHubUpdater.GetReleaseFromTag($"v{currentAppVersion.Major}.{currentAppVersion.Minor}.{currentAppVersion.Build}.{currentAppVersion.Revision}");
        }
        */

        var gitHubUpdater = new Data.GitHub.GitHubUpdater();

        // If this is a GitHub build check if there is a new version.
        var newUpdateTask = gitHubUpdater.CheckForNewGitHubRelease(false);

        await DLLManager.Instance.LoadManifestsAsync();


        // The multiplayer warning used to interrupt here, over the loading screen, about an action
        // the user had not taken. It now appears before the first write - see MultiplayerWarning.

        if (DLLManager.Instance.HasLoadedManifest() == false)
        {
            var dialog = new EasyContentDialog(RootGrid.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Error"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
                PrimaryButtonText = ResourceHelper.GetString("MainWindow_ManifestCouldNotBeLoaded_GitHubIssues"),
                SecondaryButtonText = ResourceHelper.GetString("MainWindow_ManifestCouldNotBeLoaded_UpdateManifest"),
                DefaultButton = ContentDialogButton.Primary,
                Content = ResourceHelper.GetString("MainWindow_ManifestCouldNotBeLoaded_Message"),
            };
            var shouldClose = true;

            var response = await dialog.ShowAsync();
            if (response == ContentDialogResult.Primary)
            {
                // This fork's tracker, not upstream's. This fork publishes its own manifest, so a
                // manifest failure here is plausibly this fork's pipeline - reporting it upstream
                // would send the reader to people who cannot see the file that failed.
                await Launcher.LaunchUriAsync(new Uri("https://github.com/dkflint723/swapshelf/issues"));
            }
            else if (response is ContentDialogResult.Secondary)
            {
                dialog = new EasyContentDialog(RootGrid.XamlRoot)
                {
                    Title = ResourceHelper.GetString("MainWindow_AttemptingManifestUpdate"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = new ProgressRing()
                    {
                        IsActive = true,
                        IsIndeterminate = true,
                    },
                };

                var updateTask = DLLManager.Instance.UpdateManifestAsync();
                _ = dialog.ShowAsync();
                await updateTask;
                dialog.Hide();

                if (DLLManager.Instance.HasLoadedManifest() == true)
                {
                    shouldClose = false;
                }
            }

            if (shouldClose)
            {
                dialog = new EasyContentDialog(RootGrid.XamlRoot)
                {
                    Title = ResourceHelper.GetString("MainWindow_DlssSwapperMustClose"),
                    CloseButtonText = ResourceHelper.GetString("General_Close"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetString("MainWindow_DlssSwapperCloseDueToManifest"),
                };
                await dialog.ShowAsync();

                Close();
            }
        }

        if (DLLManager.Instance.ImportedManifest is null)
        {
            var dialog = new EasyContentDialog(RootGrid.XamlRoot)
            {
                Title = ResourceHelper.GetString("LibraryPage_CouldNotLoadImportedDlls"),
                DefaultButton = ContentDialogButton.Close,
                Content = new ImportSystemDisabledView(),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
            };
            await dialog.ShowAsync();
        }

        // Yeet this into the void and let it load in the background.
        _ = DLLManager.Instance.UpdateManifestAsync();

        // Keep checking while the app is open, otherwise the games list only reflects what was
        // available when it started.
        DLLManager.Instance.StartPeriodicManifestCheck();

        // We are now ready to show the games list.
        LoadingStackPanel.Visibility = Visibility.Collapsed;

        GoToPage(GameGridPage.PageTag);

        // The sidebar counts were built with its control, which is long before any game exists, so
        // they have to be taken again now there is a library to count.
        RefreshSidebar();

        // TODO: Disabled because CommunityToolkit.WinUI.Helpers.SystemInformation.Instance.IsAppUpdated throws exceptions for unpackaged apps.
        /*
        if (releaseNotesTask is not null)
        {
            await releaseNotesTask;
            if (releaseNotesTask.Result is not null)
            {
                gitHubUpdater?.DisplayWhatsNewDialog(releaseNotesTask.Result, RootGrid);
            }
        }
        */

        // TODO: What happens if you have no internet
        await newUpdateTask;
        if (newUpdateTask.Result is not null)
        {
            if (gitHubUpdater.HasPromptedBefore(newUpdateTask.Result) == false)
            {
                await gitHubUpdater.DisplayNewUpdateDialog(newUpdateTask.Result, RootGrid.XamlRoot);
            }
        }
    }

}



