using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DLSS_Swapper.UserControls;

public sealed partial class CoverArtPicker : UserControl
{
    public CoverArtPickerModel ViewModel { get; private set; }

    public CoverArtPicker(Game game) : this(new CoverArtPickerModel(game))
    {
    }

    /// <summary>
    /// Shows a picker somebody else owns.
    /// </summary>
    /// <remarks>
    /// The library scan works down its list of uncertain games and needs the model to outlive each
    /// view, so it builds the model and hands it here. Same control, same behaviour - the point of
    /// reusing it is that the scan's picker cannot drift from the game page's.
    /// </remarks>
    public CoverArtPicker(CoverArtPickerModel model)
    {
        ViewModel = model;

        InitializeComponent();
    }

    /// <summary>
    /// Enter searches, because a search box that needs the mouse to submit is a search box nobody
    /// uses twice.
    /// </summary>
    void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;

        if (ViewModel.SearchCommand.CanExecute(null))
        {
            ViewModel.SearchCommand.Execute(null);
        }
    }
}
