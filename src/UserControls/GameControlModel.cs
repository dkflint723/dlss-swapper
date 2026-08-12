using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.IO;
using Windows.System;
using DLSS_Swapper.Helpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DLSS_Swapper.Data.DLSS;
using System.ComponentModel;

namespace DLSS_Swapper.UserControls;

public partial class GameControlModel : ObservableObject
{
    WeakReference<Pages.GameDetailPage> gameControlWeakReference;

    public Game Game { get; init; }

    public bool IsManuallyAdded => Game.GameLibrary == Interfaces.GameLibrary.ManuallyAdded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameTitleHasChanged))]
    public partial string GameTitle { get; set; }

    [ObservableProperty]
    public partial PresetOption? SelectedDlssPreset { get; set; }

    [ObservableProperty]
    public partial PresetOption? SelectedDlssDPreset { get; set; }

    [ObservableProperty]
    public partial PresetOption? SelectedDlssGPreset { get; set; }

    public bool CanSelectDlssPreset { get; private set; }

    public bool CanSelectDlssDPreset { get; private set; }

    public bool CanSelectDlssGPreset { get; private set; }

    public List<PresetOption> DlssPresetOptions { get; } = new List<PresetOption>();

    public List<PresetOption> DlssDPresetOptions { get; } = new List<PresetOption>();

    public List<PresetOption> DlssGPresetOptions { get; } = new List<PresetOption>();

    PresetOption? _previousDlssPreset;
    PresetOption? _previousDlssDPreset;
    PresetOption? _previousDlssGPreset;

    public bool GameTitleHasChanged
    {
        get
        {
            if (IsManuallyAdded == false)
            {
                return false;
            }

            if (string.IsNullOrEmpty(GameTitle))
            {
                return false;
            }

            return GameTitle.Equals(Game.Title) == false;
        }
    }

    public GameControlModelTranslationProperties TranslationProperties { get; } = new GameControlModelTranslationProperties();

    /// <summary>
    /// Whether each preset control is shown.
    /// </summary>
    /// <remarks>
    /// A preset belongs to a dll, so it has to disappear with it. Hiding only the pickers left
    /// their partners stranded: a game with no DLSS showed three preset dropdowns reading "Not
    /// supported" beside an empty column. These are computed once here rather than combined in the
    /// binding, because x:Bind cannot express "has the dll and the preset is selectable".
    /// </remarks>
    [ObservableProperty]
    public partial Visibility DlssPresetVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility DlssDPresetVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility DlssGPresetVisibility { get; set; } = Visibility.Collapsed;

    static Visibility VisibleIfPresent(GameEngineSplit engines, GameAssetType assetType)
    {
        return engines.Present.Contains(assetType) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Whether the presets section has any row in it at all.</summary>
    public Visibility AnyPresetVisibility => DlssPresetVisibility == Visibility.Visible
        || DlssDPresetVisibility == Visibility.Visible
        || DlssGPresetVisibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;

    /// <summary>
    /// What a preset is, or which of the three reasons it cannot be set.
    /// </summary>
    /// <remarks>
    /// The three were distinguishable all along — no NVIDIA driver, no driver profile for this
    /// game, a permission problem — and were collapsed into one word, "Not supported", beside a
    /// dropdown that had greyed itself out. An error icon appeared for exactly one of the three.
    /// A disabled control with no reason reads as broken rather than as unavailable.
    /// </remarks>
    public string DlssPresetDescription => PresetAvailability.Describe(
        NVAPIHelper.Instance.IsSupported,
        NVAPIHelper.Instance.PermissionIssue,

        // The driver profile is what CanSelect is set from, a few lines further down the
        // constructor: it is only ever true once FindGameProfile has returned one.
        CanSelectDlssPreset || CanSelectDlssDPreset || CanSelectDlssGPreset);

    /// <summary>
    /// Hides the buttons that change dlls when the game is locked.
    /// </summary>
    /// <remarks>
    /// The swap path refuses anyway, but a button whose only outcome is a refusal is worse than no
    /// button: it invites the click and then explains why it was wrong.
    /// </remarks>
    [ObservableProperty]
    public partial Visibility CanChangeDllsVisibility { get; set; } = Visibility.Visible;

    /// <summary>
    /// Shown only for libraries this app can actually start a game through.
    /// </summary>
    /// <remarks>
    /// The same reasoning as the note above, applied to the button that had been ignoring it:
    /// Launch was always shown, and for a library that cannot be launched from its only possible
    /// outcome was an error dialog saying so. <see cref="GameManager.CanLaunchGame"/> knows the
    /// answer before the click.
    /// </remarks>
    public Visibility CanLaunchVisibility => GameManager.Instance.CanLaunchGame(Game)
        ? Visibility.Visible
        : Visibility.Collapsed;

    /// <summary>
    /// Shown only when there is something to update.
    /// </summary>
    /// <remarks>
    /// It used to be offered whatever the game's state, and on an up-to-date game it did nothing
    /// and said nothing.
    /// </remarks>
    public Visibility HasUpdatesVisibility => CanChangeDllsVisibility == Visibility.Visible
        && Game.OutdatedAssetTypes.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    /// <summary>Reads as "Never update this game", checked when it is set.</summary>
    public bool SkipUpdates => Game.SkipUpdates;

    /// <summary>
    /// The setting that greys out every row on this page, finally reachable from it.
    /// </summary>
    /// <remarks>
    /// It was only ever settable by right-clicking the game on another page, which the lock's own
    /// tooltip had to tell people to go and do.
    /// </remarks>
    [RelayCommand]
    async Task ToggleSkipUpdatesAsync()
    {
        Game.SkipUpdates = Game.SkipUpdates == false;
        await Game.SaveToDatabaseAsync();

        CanChangeDllsVisibility = Game.SkipUpdates ? Visibility.Collapsed : Visibility.Visible;

        OnPropertyChanged(nameof(SkipUpdates));
        OnPropertyChanged(nameof(HasUpdatesVisibility));

        // Every row's sentence says whether the game is behind, and a locked game is not.
        RefreshUpscalerRows();

        // The Hidden and "Have an update" counts are taken from the library, and this changes what
        // one of them contains.
        App.CurrentApp.MainWindow?.GameGridPage?.ViewModel.RefreshFilterTabs();
    }

    /// <summary>Names the upscalers this game does not have, in place of eight empty pickers.</summary>
    [ObservableProperty]
    public partial string NotPresentSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility NotPresentSummaryVisibility { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// One row per upscaler this game actually has.
    /// </summary>
    /// <remarks>
    /// Nine hardcoded controls before this, each deciding for itself whether the game had its dll,
    /// which is <see cref="GameEngines.Split"/>'s question asked a second time. These come from
    /// <see cref="UpscalerRows.For"/>, which produces the rows and the "not in this game" line from
    /// one answer.
    /// </remarks>
    public ObservableCollection<UpscalerRowStatus> UpscalerRowList { get; } = new ObservableCollection<UpscalerRowStatus>();

    /// <summary>
    /// Rebuilds the rows and the line naming what is missing.
    /// </summary>
    /// <remarks>
    /// Called whenever something has written to the game, because every row's sentence is about
    /// what is on disk right now — which version, whether an original was kept, whether there are
    /// two copies. A row built once at construction goes stale the first time a swap succeeds.
    /// </remarks>
    void RefreshUpscalerRows()
    {
        var rows = UpscalerRows.For(Game);

        UpscalerRowList.Clear();
        foreach (var row in rows.Rows)
        {
            UpscalerRowList.Add(row);
        }

        NotPresentSummary = rows.AbsentSummary;

        // A game with no upscalers at all would otherwise get a line listing all nine, which is
        // just a long way of saying the app has nothing to do here.
        NotPresentSummaryVisibility = string.IsNullOrEmpty(rows.AbsentSummary) == false && rows.Rows.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public GameControlModel(Pages.GameDetailPage gameControl, Game game) : base()
    {
        gameControlWeakReference = new WeakReference<Pages.GameDetailPage>(gameControl);
        Game = game;
        GameTitle = game.Title;

        CanChangeDllsVisibility = game.SkipUpdates ? Visibility.Collapsed : Visibility.Visible;

        var engines = GameEngines.Split(game);

        DlssPresetVisibility = VisibleIfPresent(engines, GameAssetType.DLSS);
        DlssDPresetVisibility = VisibleIfPresent(engines, GameAssetType.DLSS_D);
        DlssGPresetVisibility = VisibleIfPresent(engines, GameAssetType.DLSS_G);

        RefreshUpscalerRows();


        // Make sure NVAPIHelper is supported and the game has DLSS.
        if (NVAPIHelper.Instance.IsSupported && game.GetAssetSlot(GameAssetType.DLSS)?.CurrentAsset is not null)
        {
            // Try load the DriverSettingProfile for the given game. If it is not found the game is not supported.

            var gameProfile = NVAPIHelper.Instance.FindGameProfile(game);
            if (gameProfile is not null)
            {
                var gameDLSSPresetResult = NVAPIHelper.Instance.GetGameDLSSPreset(game);
                if (gameDLSSPresetResult.Success)
                {
                    CanSelectDlssPreset = true;
                    game.DlssPreset = gameDLSSPresetResult.Result;
                    DlssPresetOptions.AddRange(NVAPIHelper.Instance.DlssPresetOptions);
                    if (game.DlssPreset is null)
                    {
                        // If it was never set, ensure it goes to default.
                        SelectedDlssPreset = DlssPresetOptions.FirstOrDefault(x => x.Value == 0);
                    }
                    else
                    {
                        SelectedDlssPreset = DlssPresetOptions.FirstOrDefault(x => x.Value == game.DlssPreset);
                    }


                    if (Game.GetAssetSlot(GameAssetType.DLSS_D)?.CurrentAsset is not null)
                    {
                        var gameDLSSDPresetResult = NVAPIHelper.Instance.GetGameDLSSDPreset(game);
                        if (gameDLSSDPresetResult.Success)
                        {
                            CanSelectDlssDPreset = true;

                            game.DlssDPreset = gameDLSSDPresetResult.Result;
                            DlssDPresetOptions.AddRange(NVAPIHelper.Instance.DlssDPresetOptions);
                            if (game.DlssDPreset is null)
                            {
                                // If it was never set, ensure it goes to default.
                                SelectedDlssDPreset = DlssDPresetOptions.FirstOrDefault(x => x.Value == 0);
                            }
                            else
                            {
                                SelectedDlssDPreset = DlssDPresetOptions.FirstOrDefault(x => x.Value == game.DlssDPreset);
                            }
                        }
                    }


                    if (Game.GetAssetSlot(GameAssetType.DLSS_G)?.CurrentAsset is not null)
                    {
                        var gameDLSSGPresetResult = NVAPIHelper.Instance.GetGameDLSSGPreset(game);
                        if (gameDLSSGPresetResult.Success)
                        {
                            CanSelectDlssGPreset = true;

                            game.DlssGPreset = gameDLSSGPresetResult.Result;
                            DlssGPresetOptions.AddRange(NVAPIHelper.Instance.DlssGPresetOptions);
                            if (game.DlssGPreset is null)
                            {
                                // If it was never set, ensure it goes to default.
                                SelectedDlssGPreset = DlssGPresetOptions.FirstOrDefault(x => x.Value == 0);
                            }
                            else
                            {
                                SelectedDlssGPreset = DlssGPresetOptions.FirstOrDefault(x => x.Value == game.DlssGPreset);
                            }
                        }
                    }
                }
            }
        }

        if (CanSelectDlssPreset == false)
        {
            var disabledPresetOption = new PresetOption(ResourceHelper.GetString("General_NotSupported"), 0);
            DlssPresetOptions.Add(disabledPresetOption);
            SelectedDlssPreset = disabledPresetOption;
        }

        if (CanSelectDlssDPreset == false)
        {
            var disabledPresetOption = new PresetOption(ResourceHelper.GetString("General_NotSupported"), 0);
            DlssDPresetOptions.Add(disabledPresetOption);
            SelectedDlssDPreset = disabledPresetOption;
        }

        if (CanSelectDlssGPreset == false)
        {
            var disabledPresetOption = new PresetOption(ResourceHelper.GetString("General_NotSupported"), 0);
            DlssGPresetOptions.Add(disabledPresetOption);
            SelectedDlssGPreset = disabledPresetOption;
        }
    }

    partial void OnSelectedDlssPresetChanging(PresetOption? value)
    {
        _previousDlssPreset = SelectedDlssPreset;
    }

    partial void OnSelectedDlssDPresetChanging(PresetOption? value)
    {
        _previousDlssDPreset = SelectedDlssDPreset;
    }

    partial void OnSelectedDlssGPresetChanging(PresetOption? value)
    {
        _previousDlssGPreset = SelectedDlssGPreset;
    }


    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(SelectedDlssPreset))
        {
            WritePreset(
                CanSelectDlssPreset,
                SelectedDlssPreset,
                Game.DlssPreset,
                value => NVAPIHelper.Instance.SetGameDLSSPreset(Game, value).Success,
                () => SelectedDlssPreset = _previousDlssPreset);
        }
        else if (e.PropertyName == nameof(SelectedDlssDPreset))
        {
            WritePreset(
                CanSelectDlssDPreset,
                SelectedDlssDPreset,
                Game.DlssDPreset,
                value => NVAPIHelper.Instance.SetGameDLSSDPreset(Game, value).Success,
                () => SelectedDlssDPreset = _previousDlssDPreset);
        }
        else if (e.PropertyName == nameof(SelectedDlssGPreset))
        {
            WritePreset(
                CanSelectDlssGPreset,
                SelectedDlssGPreset,
                Game.DlssGPreset,
                value => NVAPIHelper.Instance.SetGameDLSSGPreset(Game, value).Success,
                () => SelectedDlssGPreset = _previousDlssGPreset);
        }
    }

    /// <summary>
    /// Writes one preset to the driver, and puts the dropdown back if the driver refuses.
    /// </summary>
    /// <remarks>
    /// One copy instead of three. The guard, the call, the failure check, the rollback and the
    /// error dialog were written out once per preset kind, which is three chances for one of them
    /// to drift. The guard itself is <see cref="PresetAvailability.ShouldWrite"/>, which is the
    /// part that can be run in a test — everything left here needs a driver and a window.
    ///
    /// The rollback goes through the dispatcher because it is assigning to the property whose
    /// change notification is currently running.
    /// </remarks>
    void WritePreset(bool canSet, PresetOption? selected, uint? currentValue, Func<uint, bool> write, Action rollback)
    {
        if (PresetAvailability.ShouldWrite(canSet, selected?.Value, currentValue) == false)
        {
            return;
        }

        if (write(selected!.Value))
        {
            return;
        }

        if (gameControlWeakReference.TryGetTarget(out Pages.GameDetailPage? gameControl))
        {
            gameControl.DispatcherQueue.TryEnqueue(() => rollback());
            _ = NVAPIHelper.Instance.DisplayNVAPIErrorAsync(gameControl.XamlRoot);
        }
    }

    [RelayCommand]
    async Task OpenInstallPathAsync()
    {
        try
        {
            if (Directory.Exists(Game.InstallPath))
            {
                Process.Start("explorer.exe", Game.InstallPath);
            }
            else
            {
                throw new Exception(ResourceHelper.GetFormattedResourceTemplate("GamePage_CouldNotFindGameInstallPathTemplate", Game.InstallPath));
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);

            if (gameControlWeakReference.TryGetTarget(out Pages.GameDetailPage? gameControl))
            {
                var dialog = new EasyContentDialog(gameControl.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    Content = err.Message,
                };
                await dialog.ShowAsync();
            }
        }
    }

    [RelayCommand]
    async Task LaunchAsync()
    {
        if (GameManager.Instance.CanLaunchGame(Game))
        {
            await GameManager.Instance.LaunchGameAsync(Game);
        }
        else
        {
            if (gameControlWeakReference.TryGetTarget(out Pages.GameDetailPage? gameControl))
            {
                var dialog = new EasyContentDialog(gameControl.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetFormattedResourceTemplate("GamePage_CantLaunchFromLibraryTemplate", Game.GameLibrary),
                };
                await dialog.ShowAsync();
            }
        }
    }

    [RelayCommand]
    async Task EditNotesAsync()
    {
        if (gameControlWeakReference.TryGetTarget(out Pages.GameDetailPage? gameControl))
        {
            var textBox = new TextBox()
            {
                MinHeight = 400,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
            };
            // This needs to be set after AcceptsReturn otherwise it will strip out the \r
            textBox.Text = Game.Notes;

            var dialog = new EasyContentDialog(gameControl.XamlRoot)
            {
                Title = $"{ResourceHelper.GetString("GamePage_Notes")} - {Game.Title}",
                PrimaryButtonText = ResourceHelper.GetString("General_Save"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                Content = textBox,
            };
            dialog.Resources["ContentDialogMinWidth"] = 700;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                Game.Notes = textBox.Text ?? string.Empty;
                await Game.SaveToDatabaseAsync();
            }
        }
    }

    [RelayCommand]
    async Task ViewHistoryAsync()
    {
        if (gameControlWeakReference.TryGetTarget(out var control))
        {
            var dialog = new EasyContentDialog(control.XamlRoot)
            {
                Title = $"{ResourceHelper.GetFormattedResourceTemplate("GamePage_History")} - {Game.Title}",
                PrimaryButtonText = ResourceHelper.GetString("General_Close"),
                DefaultButton = ContentDialogButton.Primary,
                Content = new GameHistoryControl(Game),
            };
            dialog.Resources["ContentDialogMinWidth"] = 800;

            await dialog.ShowAsync();
        }
    }

    [RelayCommand]
    async Task AddCoverImageAsync()
    {
        if (Game.CoverImage == Game.ExpectedCustomCoverImage)
        {
            await Game.PromptToRemoveCustomCover();
            return;
        }

        Game.PromptToBrowseCustomCover();
    }

    /// <summary>
    /// Goes back to the games list.
    /// </summary>
    /// <remarks>
    /// This used to hide a dialog. As a page there is nothing to hide, so it navigates — which is
    /// also why the games page is cached: coming back lands where you left, with the same scroll
    /// position and the same tab.
    /// </remarks>
    [RelayCommand]
    void Close()
    {
        App.CurrentApp.MainWindow?.GoToGames();
    }

    [RelayCommand]
    async Task RemoveAsync()
    {
        if (gameControlWeakReference.TryGetTarget(out Pages.GameDetailPage? gameControl))
        {
            // This should never happen
            if (IsManuallyAdded == false)
            {
                var cantDeleteDialog = new EasyContentDialog(gameControl.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetString("GamePage_ManuallyAdded_CantBeRemoved"),
                };
                await cantDeleteDialog.ShowAsync();
                return;
            }



            // This needs to be set after AcceptsReturn otherwise it will strip out the \r
            var dialog = new EasyContentDialog(gameControl.XamlRoot)
            {
                Title = $"{ResourceHelper.GetString("General_Remove")} {Game.Title}?",
                PrimaryButtonText = ResourceHelper.GetString("General_Remove"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                Content = ResourceHelper.GetFormattedResourceTemplate("GamePage_ManuallyAdded_RemoveGameTemplate", Game.Title),
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await Game.DeleteAsync();
                GameManager.Instance.RemoveGame(Game);
                App.CurrentApp.MainWindow?.GoToGames();
            }
        }
    }

    [RelayCommand]
    async Task FavouriteAsync()
    {
        Game.IsFavourite = !Game.IsFavourite;
        await Game.SaveToDatabaseAsync();
    }

    [RelayCommand]
    async Task ChangeRecordAsync(GameAssetType gameAssetType)
    {
        if (gameControlWeakReference.TryGetTarget(out Pages.GameDetailPage? gameControl))
        {
            var dialog = new EasyContentDialog(gameControl.XamlRoot)
            {
                Title = ResourceHelper.GetFormattedResourceTemplate("GamePage_SelectDllTemplateTitle", DLLManager.Instance.GetAssetTypeName(gameAssetType)),
                PrimaryButtonText = ResourceHelper.GetString("General_Swap"),
                IsPrimaryButtonEnabled = false,
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };

            var dllPickerControl = new DLLPickerControl(dialog, Game, gameAssetType);
            dialog.Content = dllPickerControl;
            await dialog.ShowAsync();

            // The rows say what is on disk, and something just wrote to it. Without this the row
            // kept claiming the version it had before the swap — the file changed and the sentence
            // describing it did not, which is the exact failure this rebuild set out to remove.
            RefreshUpscalerRows();
            OnPropertyChanged(nameof(HasUpdatesVisibility));
        }
    }

    [RelayCommand]
    async Task SaveTitleAsync()
    {
        Game.Title = GameTitle;
        await Game.SaveToDatabaseAsync();
        OnPropertyChanged(nameof(GameTitleHasChanged));
    }

    /// <summary>
    /// Hands this game's outdated dlls to the games page's preview sheet.
    /// </summary>
    /// <remarks>
    /// It used to run <c>DllUpdatePrompt</c>: a confirmation giving only a count, then a modal
    /// progress dialog, then a summary — and no way back. The games page replaced all three with a
    /// sheet that lists exactly which files will be written and a strip that keeps what it wrote so
    /// it can put that batch back, and there was no reason this surface should not have the same.
    ///
    /// Closes first, because the sheet and the strip live on the page behind this dialog. That is
    /// the honest order: the review happens where the result will be visible.
    /// </remarks>
    [RelayCommand]
    void UpdateAllDlls()
    {
        var gameGridPageModel = App.CurrentApp.MainWindow?.GameGridPage?.ViewModel;
        if (gameGridPageModel is null)
        {
            return;
        }

        // The sheet is put up before navigating, not after. Setting it after the frame's content
        // changes left the games page showing nothing: the sheet exists on that page, and it has to
        // be there to be found when the page comes back.
        gameGridPageModel.ShowUpdatePreviewFor(Game);
        Close();
    }

    [RelayCommand]
    async Task ResetAllAsync()
    {
        if (gameControlWeakReference.TryGetTarget(out var gameControl) == false)
        {
            return;
        }

        var revertableAssetTypes = DllUpdateRunner.GetRevertableAssetTypes(Game);

        await DllUpdatePrompt.RunAsync(
            gameControl.XamlRoot,
            [Game],
            ResourceHelper.GetString("GamePage_ResetAll"),
            revertableAssetTypes.Count,
            ResourceHelper.GetFormattedResourceTemplate("DllRevert_ConfirmOneGameTemplate", revertableAssetTypes.Count, Game.Title),
            ResourceHelper.GetString("DllRevert_NothingToRevert"),
            (games, progress, cancellationToken) => DllUpdateRunner.RevertGamesAsync(games, progress, cancellationToken),
            "DllRevert_RevertedTemplate");

        // Same reason as the picker: this one has just put every dll back.
        RefreshUpscalerRows();
        OnPropertyChanged(nameof(HasUpdatesVisibility));
    }

    [RelayCommand]
    async Task MultipleDLLsFoundAsync(GameAssetType gameAssetType)
    {
        if (gameControlWeakReference.TryGetTarget(out Pages.GameDetailPage? gameControl))
        {
            var dialog = new EasyContentDialog(gameControl.XamlRoot)
            {
                Title = ResourceHelper.GetFormattedResourceTemplate("GamePage_MultipleDllsFoundTemplate", DLLManager.Instance.GetAssetTypeName(gameAssetType)),
                PrimaryButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Primary,
                Content = new MultipleDLLsFoundControl(Game, gameAssetType),
            };

            await dialog.ShowAsync();
        }
    }

    [RelayCommand]
    async Task ReadyToPlayStateMoreInformationAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/beeradmoore/dlss-swapper/wiki/Troubleshooting#game-is-not-in-a-ready-to-play-state"));
    }

    [RelayCommand]
    async Task DLSSPresetInfoAsync()
    {
        if (gameControlWeakReference.TryGetTarget(out Pages.GameDetailPage? gameControl))
        {
            var dialog = new EasyContentDialog(gameControl.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamePage_DLSSPresetInfo_Title"),
                PrimaryButtonText = ResourceHelper.GetString("General_Okay"),
                SecondaryButtonText = ResourceHelper.GetString("GamePage_DLSSPresetInfo_OnScreenIndicator"),
                DefaultButton = ContentDialogButton.Primary,
                Content = ResourceHelper.GetString("GamePage_DLSSPresetInfo_Message"),
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Secondary)
            {
                await Launcher.LaunchUriAsync(new Uri("https://github.com/beeradmoore/dlss-swapper/wiki/DLSS-Developer-Options#on-screen-indicator"));
            }
        }
    }

    TaskCompletionSource? _reloadGameTaskCompletionSource;

    [RelayCommand]
    async Task ReloadGameAsync()
    {
        if (_reloadGameTaskCompletionSource is not null)
        {
            _reloadGameTaskCompletionSource.SetCanceled();
        }

        if (gameControlWeakReference.TryGetTarget(out var control))
        {
            _reloadGameTaskCompletionSource = new TaskCompletionSource();

            Game.PropertyChanged += Game_PropertyChanged;
            Game.NeedsProcessing = true;
            Game.ProcessGame(forceNeedsProcessing: true);

            var dialogStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var dialog = new EasyContentDialog(App.CurrentApp.MainWindow.Content.XamlRoot)
            {
                Title = ResourceHelper.GetString("GamesPage_ReloadingGame"),
                Content = new ProgressRing()
                {
                    IsIndeterminate = true,
                },
                PrimaryButtonText = ResourceHelper.GetString("General_Cancel")
            };
            var dialogTask = dialog.ShowAsync().AsTask();

            await Task.WhenAny(dialogTask, _reloadGameTaskCompletionSource.Task);

            Game.PropertyChanged -= Game_PropertyChanged;


            if (dialogTask.IsCompleted)
            {
                // User clicked cancel, close the current dialog.
                Close();
            }
            else
            {
                var loadingDuration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - dialogStart;

                if (loadingDuration < 1000)
                {
                    // Force loading dialog to exist for at least 1 second
                    await Task.Delay(1000 - (int)loadingDuration);
                }

                Close();

                if (dialogTask.IsCompleted == true)
                {
                    return;
                }

                // Game finished reloading, so open it again — a fresh page rather than this one,
                // because everything it shows was rebuilt from what the rescan found.
                _reloadGameTaskCompletionSource = null;
                dialog.Hide();
                App.CurrentApp.MainWindow?.ShowGame(Game);
            }
        }
    }

    private void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Game.Processing))
        {
            if (Game.Processing == true)
            {
                _reloadGameTaskCompletionSource?.SetResult();
            }
        }
    }

    [RelayCommand]
    async Task ShowHideGameAsync()
    {
        if (Game.IsHidden is null)
        {
            Game.IsHidden = true;
        }
        else
        {
            Game.IsHidden = !Game.IsHidden;
        }
        await Game.SaveToDatabaseAsync();
    }

    [RelayCommand]
    async Task NVAPIErrorAsync()
    {
        if (gameControlWeakReference.TryGetTarget(out var control))
        {
            await NVAPIHelper.Instance.DisplayNVAPIErrorAsync(control.XamlRoot);
        }
    }
}
