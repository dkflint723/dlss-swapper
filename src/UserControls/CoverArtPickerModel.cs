using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.SteamGridDb;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// Search SteamGridDB for a cover, look at what it has, then write one.
/// </summary>
/// <remarks>
/// <para>
/// Two steps on purpose. A name search is fuzzy - "doom" matches two games called exactly "Doom" -
/// so the first step is picking which game it is, and only then is there art to look at. Guessing
/// the game would mean writing a cover from whichever entry happened to rank first.
/// </para>
/// <para>
/// Holds no control. It takes the game it is about and raises <see cref="Finished"/> when the cover
/// has been written, which is what closes the dialog around it - rather than the dialog cancelling
/// its own close and waiting on a command, which is how the dll picker does it and is why that one
/// has a comment wondering whether the dialog is already closing.
/// </para>
/// </remarks>
public partial class CoverArtPickerModel : ObservableObject
{
    readonly Game _game;

    CancellationTokenSource? _cancellation;

    public CoverArtPickerModelTranslationProperties TranslationProperties { get; } = new CoverArtPickerModelTranslationProperties();

    /// <summary>Raised once a cover has been written, so the dialog can close itself.</summary>
    public event EventHandler? Finished;

    public ObservableCollection<CoverArtGameItem> Games { get; } = new ObservableCollection<CoverArtGameItem>();

    public ObservableCollection<CoverArtImageItem> Images { get; } = new ObservableCollection<CoverArtImageItem>();

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImagesVisibility))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial CoverArtImageItem? SelectedImage { get; set; }

    /// <summary>
    /// Which game the covers belong to, and with it which of the two pages is showing.
    /// </summary>
    /// <remarks>
    /// Null is the search page, set is the covers page. One piece of state rather than two, so the
    /// dialog cannot end up showing both lists or neither.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchVisibility))]
    [NotifyPropertyChangedFor(nameof(GamesVisibility))]
    [NotifyPropertyChangedFor(nameof(CoversVisibility))]
    [NotifyPropertyChangedFor(nameof(CoversHeading))]
    public partial CoverArtGameItem? SelectedGame { get; set; }

    /// <summary>What is happening, or what went wrong. Empty when there is nothing to say.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusVisibility))]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool IsBusy { get; set; }

    public Visibility StatusVisibility => string.IsNullOrEmpty(StatusText) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The search box and its results, shown until a game has been picked.</summary>
    public Visibility SearchVisibility => SelectedGame is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GamesVisibility => SelectedGame is null && Games.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The covers, which replace the search rather than appearing under it.</summary>
    public Visibility CoversVisibility => SelectedGame is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ImagesVisibility => Images.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Names the game the covers belong to, since its row is no longer on screen to check.</summary>
    public string CoversHeading => SelectedGame is null
        ? string.Empty
        : ResourceHelper.GetFormattedResourceTemplate("CoverArt_CoversForTemplate", SelectedGame.Name);

    public CoverArtPickerModel(Game game)
    {
        _game = game;

        // Pre-filled with the game's own name, because that is the search almost everybody wants and
        // typing it again is a tax on the common case. It stays editable for the times the store's
        // title and the art database's disagree.
        SearchText = game.Title;
    }

    bool CanSearch() => IsBusy == false;

    /// <summary>Finds the games whose names match, for the user to say which one this is.</summary>
    [RelayCommand(CanExecute = nameof(CanSearch))]
    async Task SearchAsync()
    {
        var token = Restart();

        Games.Clear();
        Images.Clear();
        SelectedGame = null;
        SelectedImage = null;
        OnPropertyChanged(nameof(GamesVisibility));
        OnPropertyChanged(nameof(ImagesVisibility));

        IsBusy = true;
        StatusText = ResourceHelper.GetString("CoverArt_Searching");

        try
        {
            var results = await SteamGridDbClient.SearchAsync(SearchText, token).ConfigureAwait(true);

            if (token.IsCancellationRequested)
            {
                return;
            }

            foreach (var result in results)
            {
                Games.Add(new CoverArtGameItem(result));
            }

            StatusText = Games.Count == 0 ? ResourceHelper.GetString("CoverArt_NoGames") : string.Empty;
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            StatusText = err is SteamGridDbException ? err.Message : ResourceHelper.GetString("General_Error");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(GamesVisibility));
        }
    }

    /// <summary>Loads the chosen game's covers. Runs off the selection rather than a button.</summary>
    partial void OnSelectedGameChanged(CoverArtGameItem? value)
    {
        if (value is null)
        {
            return;
        }

        _ = LoadImagesAsync(value.Id);
    }

    async Task LoadImagesAsync(int gameId)
    {
        var token = Restart();

        Images.Clear();
        SelectedImage = null;
        OnPropertyChanged(nameof(ImagesVisibility));

        IsBusy = true;
        StatusText = ResourceHelper.GetString("CoverArt_LoadingArt");

        try
        {
            var results = await SteamGridDbClient.GetPortraitsAsync(gameId, token).ConfigureAwait(true);

            if (token.IsCancellationRequested)
            {
                return;
            }

            foreach (var result in results)
            {
                Images.Add(new CoverArtImageItem(result));
            }

            StatusText = Images.Count == 0 ? ResourceHelper.GetString("CoverArt_NoArt") : string.Empty;
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            StatusText = err is SteamGridDbException ? err.Message : ResourceHelper.GetString("General_Error");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ImagesVisibility));
        }
    }

    /// <summary>
    /// Returns to the results, with the search and its matches as they were.
    /// </summary>
    /// <remarks>
    /// The one thing the covers page cannot do without: a fuzzy search means picking the wrong
    /// entry is ordinary rather than careless, and without this the only way back is to cancel the
    /// dialog and search again.
    /// </remarks>
    [RelayCommand]
    void Back()
    {
        Cancel();

        Images.Clear();
        SelectedImage = null;

        // Clearing this is what shows the search page again, and it leaves Games untouched, so the
        // matches are still there rather than needing another round trip.
        SelectedGame = null;

        StatusText = string.Empty;
        IsBusy = false;

        OnPropertyChanged(nameof(ImagesVisibility));
    }

    bool CanApply() => IsBusy == false && SelectedImage is not null;

    /// <summary>
    /// Writes the chosen cover.
    /// </summary>
    /// <remarks>
    /// Goes through <c>Game.AddCustomCover</c>, the same call the existing button and the drag and
    /// drop both use, so a cover found this way is a custom cover like any other - it takes
    /// precedence over the store's art in the same way and the existing remove takes it away again.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanApply))]
    async Task ApplyAsync()
    {
        var image = SelectedImage;
        if (image is null)
        {
            return;
        }

        var token = Restart();

        IsBusy = true;
        StatusText = ResourceHelper.GetString("CoverArt_Applied");

        try
        {
            using var stream = await SteamGridDbClient.DownloadAsync(image.Url, token).ConfigureAwait(true);

            // Off the UI thread: this decodes and resizes an image, and the dialog is still on
            // screen while it happens.
            await Task.Run(() => _game.AddCustomCover(stream), token).ConfigureAwait(true);

            Finished?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            StatusText = err is SteamGridDbException ? err.Message : ResourceHelper.GetString("General_Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Falls back to a file the user already has.
    /// </summary>
    /// <remarks>
    /// The way out when the search cannot find the game at all, which is a real outcome rather than
    /// an error: plenty of games are not in SteamGridDB and a manually added one may be nowhere.
    /// Nothing in this dialog has written anything up to this point, so the cover already in place
    /// stays exactly as it was whether this is used or not.
    ///
    /// It goes through the same <c>PromptToBrowseCustomCover</c> the game page's own button uses,
    /// so this is not a second way of setting a cover, only a second door onto the first.
    /// </remarks>
    [RelayCommand]
    void Browse()
    {
        Cancel();

        // Left open when the file picker was cancelled, so a mis-click does not throw away a search
        // that took a round trip to get.
        if (_game.PromptToBrowseCustomCover())
        {
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Abandons whatever was in flight and returns the token for what replaces it.
    /// </summary>
    /// <remarks>
    /// Searching again while a search is still running would otherwise let the older answer arrive
    /// last and overwrite the newer one, which is the same shape as five bugs this app has already
    /// had: a value written by something that no longer describes the truth.
    /// </remarks>
    CancellationToken Restart()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();

        return _cancellation.Token;
    }

    public void Cancel()
    {
        _cancellation?.Cancel();
    }
}
