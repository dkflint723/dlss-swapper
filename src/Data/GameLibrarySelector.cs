using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Data;

internal class GameLibrarySelector : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public IGameLibrary GameLibrary { get; init; }
    public string Name => GameLibrary.Name;
    /// <summary>
    /// The same words every other toggle on the settings page uses. These used to read
    /// "Steam enabled" and "Steam disabled", which named the library a second time; the row's title
    /// says which library it is now. The words themselves stay, because a switch with no label is
    /// state told by nothing but the handle's position and its colour.
    /// </summary>
    public string OffContentLabel => ResourceHelper.GetString("General_No");
    public string OnContentLabel => ResourceHelper.GetString("General_Yes");

    /// <summary>
    /// Whether this library is on the machine at all. Turning on a library that is not installed
    /// finds nothing, and the settings page gave no way to tell that apart from a library that is
    /// installed and simply has no games with upscalers in it.
    /// </summary>
    /// <remarks>
    /// Answered once, in the constructor: <see cref="IGameLibrary.IsInstalled"/> goes to the
    /// registry and the disk, and there is a row for every library on the page.
    /// </remarks>
    public string Description
    {
        get
        {
            // Games you added by hand are not installed anywhere, so neither answer means anything
            // for this one. It says "Found on this PC" otherwise, which is not false so much as not
            // about anything.
            if (GameLibrary.GameLibrary == Interfaces.GameLibrary.ManuallyAdded)
            {
                return string.Empty;
            }

            return _isInstalled
                ? ResourceHelper.GetString("SettingsPage_LibraryFound")
                : ResourceHelper.GetString("SettingsPage_LibraryNotFound");
        }
    }

    readonly bool _isInstalled;

    public bool IsEnabled
    {
        get
        {
            return GameLibrary.IsEnabled;
        }
        set
        {
            if (value == GameLibrary.IsEnabled)
            {
                return;
            }

            if (value == true)
            {
                GameLibrary.Enable();
            }
            else
            {
                GameLibrary.Disable();
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            WeakReferenceMessenger.Default.Send(new Messages.GameLibrariesStateChangedMessage());
        }
    }

    public GameLibrarySelector(IGameLibrary gameLibrary)
    {
        GameLibrary = gameLibrary;
        _isInstalled = gameLibrary.IsInstalled();
    }

    internal void ReloadLabels()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OffContentLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OnContentLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
    }
}
