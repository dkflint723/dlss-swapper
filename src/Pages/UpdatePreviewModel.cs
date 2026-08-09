using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Pages;

/// <summary>
/// The update preview sheet: what would be written, with the option to leave any of it out.
/// </summary>
/// <remarks>
/// Takes its rows rather than fetching them, so it can be built and asserted on without a window.
/// </remarks>
public partial class UpdatePreviewModel : ObservableObject
{
    public ObservableCollection<PendingDllUpdate> Updates { get; }

    public UpdatePreviewModel(IEnumerable<PendingDllUpdate> updates)
    {
        Updates = new ObservableCollection<PendingDllUpdate>(updates);

        foreach (var update in Updates)
        {
            update.PropertyChanged += OnUpdateChanged;
        }
    }

    void OnUpdateChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(PendingDllUpdate.IsSelected))
        {
            return;
        }

        // The heading and the button both count the same thing, so they are recomputed together
        // rather than each being updated by whoever unchecked a row.
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ConfirmLabel));
        OnPropertyChanged(nameof(CanConfirm));
    }

    public IReadOnlyList<PendingDllUpdate> SelectedUpdates => Updates.Where(x => x.IsSelected).ToList();

    /// <summary>
    /// Reads as "Update 7 files across 6 games?".
    /// </summary>
    /// <remarks>
    /// Counts what is checked, not what was offered, so the question always describes the button
    /// underneath it.
    /// </remarks>
    public string Title
    {
        get
        {
            var selected = SelectedUpdates;

            // Three sentences rather than one template with numbers substituted into it, because
            // "Update 1 files across 1 games?" is the sort of thing that makes a tool asking for
            // permission to write to your games look like it does not know what it is doing. One
            // file always belongs to one game, so there is no fourth case.
            if (selected.Count == 1)
            {
                return ResourceHelper.GetString("Preview_TitleOneFile");
            }

            var gameCount = selected.Select(x => x.Game).Distinct().Count();
            if (gameCount == 1)
            {
                return ResourceHelper.GetFormattedResourceTemplate("Preview_TitleOneGameTemplate", selected.Count);
            }

            return ResourceHelper.GetFormattedResourceTemplate("Preview_TitleTemplate", selected.Count, gameCount);
        }
    }

    public string Body => ResourceHelper.GetString("Preview_Body");

    public string CloseGamesFirst => ResourceHelper.GetString("Preview_CloseGamesFirst");

    public string CancelLabel => ResourceHelper.GetString("General_Cancel");

    public string ConfirmLabel
    {
        get
        {
            var selectedCount = SelectedUpdates.Count;
            return selectedCount == 1
                ? ResourceHelper.GetString("Preview_ConfirmOneFile")
                : ResourceHelper.GetFormattedResourceTemplate("Preview_ConfirmTemplate", selectedCount);
        }
    }

    /// <summary>Unchecking everything leaves nothing to do, so the button stops offering to do it.</summary>
    public bool CanConfirm => Updates.Any(x => x.IsSelected);
}
