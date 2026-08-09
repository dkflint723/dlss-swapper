using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Windows.UI.Text;

namespace DLSS_Swapper.Pages;

/// <summary>
/// One engine in the upscalers page's left column.
/// </summary>
/// <remarks>
/// A column rather than the horizontally scrolling bar it replaces. Nine engines never fitted, so
/// the ones past the edge were reachable only by scrolling a strip most people did not notice was
/// scrollable, and which named no counts.
/// </remarks>
public partial class UpscalerTypeItem : ObservableObject
{
    public required GameAssetType AssetType { get; init; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>How many versions of this engine are known about.</summary>
    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionVisibility))]
    [NotifyPropertyChangedFor(nameof(LabelWeight))]
    public partial bool IsSelected { get; set; }

    /// <summary>The accent bar flush to the left of the selected row.</summary>
    public Visibility SelectionVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    public FontWeight LabelWeight => IsSelected ? FontWeights.SemiBold : FontWeights.Normal;

    public void Refresh()
    {
        Name = DLLManager.Instance.GetAssetTypeName(AssetType);

        var records = DLLManager.Instance.GetRecords(AssetType);
        CountText = records is null || records.Count == 0
            ? string.Empty
            : records.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }
}
