using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data.SteamGridDb;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// Setting a SteamGridDB key up, at the moment somebody first needs one.
/// </summary>
/// <remarks>
/// <para>
/// The key can be pasted here rather than only in Settings, because this is where the person
/// finding out they need one is standing. Sending them to another page to type something and then
/// back again to press the button they already pressed is three steps where one will do.
/// </para>
/// <para>
/// The key is checked against the api before it is kept. Saving one that does not work would be a
/// trap with no visible exit: this prompt only appears when there is no key at all, so a mistyped
/// one means every search fails from then on with nothing offering to fix it.
/// </para>
/// </remarks>
public partial class CoverArtNoKeyModel : ObservableObject
{
    public CoverArtNoKeyModelTranslationProperties TranslationProperties { get; } = new CoverArtNoKeyModelTranslationProperties();

    /// <summary>Raised once a working key has been saved, so the dialog can close and carry on.</summary>
    public event EventHandler? Finished;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string ApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusVisibility))]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BusyVisibility))]
    [NotifyPropertyChangedFor(nameof(CanEditKey))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; set; }

    public Visibility StatusVisibility => string.IsNullOrEmpty(StatusText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// The inverse of <see cref="IsBusy"/>, as a property rather than through a converter.
    /// </summary>
    /// <remarks>
    /// BoolNegationConverter is declared in the resources of the pages that use it, not app wide,
    /// so a UserControl asking for it by StaticResource throws at load - which took the whole app
    /// down the first time this dialog was opened. A property costs nothing and cannot go missing.
    /// </remarks>
    public bool CanEditKey => IsBusy == false;

    bool CanSave() => IsBusy == false && string.IsNullOrWhiteSpace(ApiKey) == false;

    [RelayCommand(CanExecute = nameof(CanSave))]
    async Task SaveAsync()
    {
        var key = ApiKey.Trim();

        IsBusy = true;
        StatusText = ResourceHelper.GetString("CoverArt_CheckingKey");

        try
        {
            var problem = await SteamGridDbClient.ValidateKeyAsync(key).ConfigureAwait(true);

            if (problem is not null)
            {
                // The api's own words. The two anybody will actually hit are a malformed key and an
                // unrecognised one, and both are about the thing just pasted into the box above.
                StatusText = problem;
                return;
            }

            Settings.Instance.SteamGridDbApiKey = key;

            Finished?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception err)
        {
            Logger.Error(err);
            StatusText = ResourceHelper.GetString("General_Error");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
