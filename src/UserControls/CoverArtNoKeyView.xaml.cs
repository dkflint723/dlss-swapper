using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// What the cover art search shows somebody who has not set a SteamGridDB key up yet.
/// </summary>
public sealed partial class CoverArtNoKeyView : UserControl
{
    public CoverArtNoKeyModel ViewModel { get; private set; }

    public CoverArtNoKeyView()
    {
        ViewModel = new CoverArtNoKeyModel();

        InitializeComponent();
    }

    /// <summary>
    /// Enter saves, because the box is filled by pasting and the next thing a hand does after
    /// Ctrl+V is press Enter.
    /// </summary>
    void KeyBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;

        if (ViewModel.SaveCommand.CanExecute(null))
        {
            ViewModel.SaveCommand.Execute(null);
        }
    }
}
