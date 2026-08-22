using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.Pages;

/// <summary>
/// The strip along the bottom of the games list: what a batch is doing, then what it did.
/// </summary>
/// <remarks>
/// One model for both, because they are one thing over time. A run that ends is not replaced by a
/// different notice; it is the same strip saying the run is over and offering to put it back.
///
/// Holds no controls, so what it says is testable without a window.
/// </remarks>
public partial class UpdateBatchModel : ObservableObject
{
    /// <summary>Whatever was written, so the strip can offer to undo exactly that.</summary>
    internal IReadOnlyList<DllWorkItem> WrittenItems { get; private set; } = new List<DllWorkItem>();

    IReadOnlyList<string> _failures = new List<string>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(RunningVisibility))]
    [NotifyPropertyChangedFor(nameof(DoneVisibility))]
    public partial bool IsDone { get; set; }

    public bool IsRunning => IsDone == false;

    public Visibility RunningVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DoneVisibility => IsDone ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Reads as "Updating 3 of 7".</summary>
    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    /// <summary>Reads as "Cyberpunk 2077 — FSR 3.1 DirectX 12".</summary>
    [ObservableProperty]
    public partial string CurrentItemText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    /// <summary>
    /// Says the stop will be clean before it is pressed.
    /// </summary>
    /// <remarks>
    /// The existing cancel already finished the file it was on and only said so afterwards, in a
    /// string nobody saw until they had already worried. The button says it up front instead.
    /// </remarks>
    [ObservableProperty]
    public partial string StopLabel { get; set; } = ResourceHelper.GetString("Update_StopAfterThisOne");

    [ObservableProperty]
    public partial bool CanStop { get; set; } = true;

    /// <summary>Reads as "7 files updated across 6 games", or the partial form when some failed.</summary>
    [ObservableProperty]
    public partial string DoneText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailures))]
    [NotifyPropertyChangedFor(nameof(FailuresVisibility))]
    public partial string DoneDetailText { get; set; } = string.Empty;

    /// <summary>The glyph beside the outcome: a tick, or the attention mark when something failed.</summary>
    [ObservableProperty]
    public partial string DoneGlyph { get; set; } = "\uE73E";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UndoVisibility))]
    public partial bool CanUndo { get; set; }

    public Visibility UndoVisibility => CanUndo ? Visibility.Visible : Visibility.Collapsed;

    public bool HasFailures => _failures.Count > 0;

    public Visibility FailuresVisibility => HasFailures ? Visibility.Visible : Visibility.Collapsed;

    public IReadOnlyList<string> Failures => _failures;

    public string UndoLabel => ResourceHelper.GetString("Update_UndoAll");

    public string SeeWhatFailedLabel => ResourceHelper.GetString("Update_SeeWhatFailed");

    public string SeeWhatChangedLabel => ResourceHelper.GetString("Update_SeeWhatChanged");

    /// <summary>
    /// The strip stays after a run so the undo is still there to press, so something has to say how
    /// to get rid of it. This was an unlabelled 12px cross, which is the one thing this app is
    /// meant not to do: it was reported as the strip never going away.
    /// </summary>
    public string DismissLabel => ResourceHelper.GetString("Update_Dismiss");

    IReadOnlyList<DllChange> _changes = new List<DllChange>();

    /// <summary>What each file was before and after, for the strip to offer to show.</summary>
    internal IReadOnlyList<DllChange> Changes => _changes;

    public bool HasChanges => _changes.Count > 0;

    /// <summary>
    /// Shown only when nothing failed.
    /// </summary>
    /// <remarks>
    /// This and "see what failed" are the same slot on the strip, so the rule that keeps them apart
    /// has to be here rather than in the two bindings, which would both be true of a partial batch
    /// and draw one button on top of the other. Failures win: a batch that half worked needs the
    /// list of what did not before the list of what did.
    /// </remarks>
    public Visibility ChangesVisibility => HasChanges && HasFailures == false
        ? Visibility.Visible
        : Visibility.Collapsed;

    internal void Report(DllUpdateProgress progress)
    {
        ProgressText = ResourceHelper.GetFormattedResourceTemplate("Update_ProgressTemplate", progress.CurrentIndex, progress.TotalCount);
        CurrentItemText = progress.Description;

        // Counts the file being started, not the one just finished, so the bar and the sentence
        // beside it are talking about the same file.
        ProgressValue = progress.TotalCount == 0 ? 0 : (progress.CurrentIndex - 1) * 100.0 / progress.TotalCount;
    }

    /// <summary>
    /// Turns the running strip into the done strip.
    /// </summary>
    /// <remarks>
    /// A batch where nothing was written still reports, rather than the strip vanishing: "nothing
    /// happened" is an outcome, and silently disappearing looks like the app forgot.
    /// </remarks>
    internal void Complete(DllUpdateResult result)
    {
        WrittenItems = result.Succeeded;
        _failures = result.Failures;
        _changes = result.Changes;

        var written = result.Succeeded.Count;

        if (result.Failures.Count > 0)
        {
            // A glyph and a sentence, never a colour: half a batch failing has to be as readable to
            // someone who cannot separate red from green as to anyone else.
            DoneGlyph = "\uE7BA";
            DoneText = ResourceHelper.GetFormattedResourceTemplate(
                "Update_DonePartialTemplate", written, written + result.Failures.Count, result.Failures.Count);
        }
        else if (written == 0)
        {
            DoneGlyph = "\uE7BA";
            DoneText = ResourceHelper.GetString("Update_DoneNothing");
        }
        else if (written == 1)
        {
            DoneGlyph = "\uE73E";
            DoneText = ResourceHelper.GetFormattedResourceTemplate("Update_DoneOneFileTemplate", result.Succeeded[0].Game.Title);
        }
        else
        {
            DoneGlyph = "\uE73E";
            DoneText = ResourceHelper.GetFormattedResourceTemplate("Update_DoneTemplate", written, result.GamesUpdated);
        }

        DoneDetailText = written > 0
            ? ResourceHelper.GetString("Update_DoneReassurance")
            : string.Empty;

        CanUndo = written > 0;
        IsDone = true;

        OnPropertyChanged(nameof(HasFailures));
        OnPropertyChanged(nameof(FailuresVisibility));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(ChangesVisibility));
    }

    /// <summary>
    /// Reports the result of putting a batch back.
    /// </summary>
    /// <remarks>
    /// The same strip again rather than a new one, and with no second undo: undoing the undo is a
    /// redo, and this is not the place to invent one.
    /// </remarks>
    internal void CompleteUndo(DllUpdateResult result)
    {
        WrittenItems = new List<DllWorkItem>();
        _failures = result.Failures;

        // The batch that was there to look at has just been put back, so there is nothing left for
        // "see what changed" to describe. Its own changes are the reverse of what was undone, which
        // is not a list anyone asked for.
        _changes = new List<DllChange>();

        DoneGlyph = result.Failures.Count > 0 ? "\uE7BA" : "\uE73E";
        DoneText = ResourceHelper.GetFormattedResourceTemplate("Update_UndoneTemplate", result.Succeeded.Count);
        DoneDetailText = string.Empty;
        CanUndo = false;
        IsDone = true;

        OnPropertyChanged(nameof(HasFailures));
        OnPropertyChanged(nameof(FailuresVisibility));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(ChangesVisibility));
    }
}
