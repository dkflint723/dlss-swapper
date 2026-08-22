using System.Collections.Generic;
using System.ComponentModel;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

public sealed partial class CoverScanDialog : UserControl
{
    public CoverScanModel ViewModel { get; private set; }

    public CoverScanDialog(IReadOnlyList<Game> games)
    {
        ViewModel = new CoverScanModel(games);

        InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    /// <summary>
    /// Puts a picker on screen while the model has one, and takes it away again after.
    /// </summary>
    /// <remarks>
    /// The control is built here rather than bound to, because the model deliberately holds view
    /// models and never controls - the same rule the rest of this app's models follow. Rebuilt
    /// per game rather than reused: <see cref="CoverArtPicker"/> reads its model once, in its
    /// constructor, so handing the same control a second model would show the first game's search.
    /// </remarks>
    void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CoverScanModel.ActivePicker))
        {
            return;
        }

        PickerHost.Content = ViewModel.ActivePicker is null
            ? null
            : new CoverArtPicker(ViewModel.ActivePicker);
    }
}
