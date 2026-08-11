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

    /// <summary>
    /// The number beside this engine's name, counting what its list would actually show.
    /// </summary>
    /// <remarks>
    /// Through the same predicate the list uses, which it was not before: this counted the raw
    /// collection while the list hid debug files, so DLSS read 107 over a list of 88.
    ///
    /// It follows the search too. The number is printed on the button that opens the list, so a
    /// column reading "XeSS 15" that opens onto nothing is the count disagreeing with its own list;
    /// and a search across nine engines is only usable if the column says which ones have matches.
    /// </remarks>
    public void Refresh(string? searchText = null)
    {
        Name = DLLManager.Instance.GetAssetTypeName(AssetType);

        var records = DLLManager.Instance.GetRecords(AssetType);
        var count = DllSearch.Count(records, searchText, Settings.Instance.AllowDebugDlls);

        // Blank when the engine has nothing at all, but an explicit zero while a search is on: an
        // empty count there reads as a number that failed to arrive rather than as "none".
        CountText = (records is null || records.Count == 0) && string.IsNullOrWhiteSpace(searchText)
            ? string.Empty
            : count.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }
}
