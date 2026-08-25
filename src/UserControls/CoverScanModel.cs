using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
/// Looks for a cover for every game at once, applies only the certain ones, and names the rest.
/// </summary>
/// <remarks>
/// <para>
/// The whole design follows from one fact: a name search is fuzzy. Doing this per game, the picker
/// asks which of the results is right. Doing it across a library, nobody wants to answer that two
/// dozen times - so this only ever proposes a cover where the name matches beyond doubt, and every
/// other game comes back by name with a reason, to be done through the picker.
/// </para>
/// <para>
/// Nothing is written by the scan itself. The list is shown first, with a thumbnail and the name
/// SteamGridDB used, and applying is a separate press - the same review-then-write the dll updates
/// have.
/// </para>
/// </remarks>
public partial class CoverScanModel : ObservableObject
{
    readonly IReadOnlyList<Game> _games;

    CancellationTokenSource? _cancellation;

    /// <summary>What was written and what it replaced, so the whole batch can be put back.</summary>
    readonly List<(Game Game, string? BackupPath)> _applied = new List<(Game, string?)>();

    public CoverScanModelTranslationProperties TranslationProperties { get; } = new CoverScanModelTranslationProperties();

    public ObservableCollection<CoverScanReadyItem> Ready { get; } = new ObservableCollection<CoverScanReadyItem>();

    public ObservableCollection<CoverScanNeedsYouItem> NeedsYou { get; } = new ObservableCollection<CoverScanNeedsYouItem>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IntroVisibility))]
    [NotifyPropertyChangedFor(nameof(ResultsVisibility))]
    public partial bool HasScanned { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressVisibility))]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Set the moment stop is pressed, so the button can say so.
    /// </summary>
    /// <remarks>
    /// A scan finishes the game it is on before it comes back, which is up to twenty seconds if that
    /// request is the one that stalled. Without this the button simply greyed out and the count sat
    /// where it was, which reads like the press was missed.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StopLabel))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial bool IsStopping { get; set; }

    /// <summary>
    /// Whether the work running right now is work a token can stop.
    /// </summary>
    /// <remarks>
    /// Not the same as busy. Undo is a local file copy loop that reads no token, so offering Stop
    /// during it would have been a button that said "Stopping..." and then did nothing at all.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StopVisibility))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial bool CanBeStopped { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    /// <summary>How many games have been reached, for a bar that shows how far along it is.</summary>
    /// <remarks>
    /// The bar used to be indeterminate. A library scan is one request a game with a pause between,
    /// so a hundred games is minutes of a bar that says only "something is happening" - and there is
    /// a real count available to say how much of it is left.
    /// </remarks>
    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial double ProgressMaximum { get; set; } = 1;

    public Visibility StopVisibility => CanBeStopped ? Visibility.Visible : Visibility.Collapsed;

    public string StopLabel => IsStopping
        ? ResourceHelper.GetString("CoverScan_Stopping")
        : ResourceHelper.GetString("CoverScan_Stop");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusVisibility))]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UndoVisibility))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    public partial bool HasApplied { get; set; }

    public Visibility IntroVisibility => HasScanned ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ResultsVisibility => HasScanned && ActivePicker is null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The picker for one uncertain game, which replaces the list while it is open.</summary>
    public Visibility PickerVisibility => ActivePicker is null ? Visibility.Collapsed : Visibility.Visible;

    public string PickerHeading => PickingFor is null
        ? string.Empty
        : ResourceHelper.GetFormattedResourceTemplate("CoverScan_PickForTemplate", PickingFor.Title);

    public Visibility ProgressVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StatusVisibility => string.IsNullOrEmpty(StatusText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ReadyVisibility => Ready.Count > 0 && ActivePicker is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NeedsYouVisibility => NeedsYou.Count > 0 && ActivePicker is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility UndoVisibility => HasApplied ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Reads "3 covers ready to apply", or says plainly that none were certain.</summary>
    public string ReadyHeading => Ready.Count switch
    {
        0 => ResourceHelper.GetString("CoverScan_NothingReady"),
        1 => ResourceHelper.GetString("CoverScan_ReadyHeaderOne"),
        _ => ResourceHelper.GetFormattedResourceTemplate("CoverScan_ReadyHeaderTemplate", Ready.Count),
    };

    /// <summary>Counts the ticked ones, so the button says what pressing it does.</summary>
    public string ApplyLabel
    {
        get
        {
            var selected = Ready.Count(x => x.IsSelected);

            return selected == 1
                ? ResourceHelper.GetString("CoverScan_ApplyOne")
                : ResourceHelper.GetFormattedResourceTemplate("CoverScan_ApplyTemplate", selected);
        }
    }

    /// <summary>
    /// The picker open over the list, or null when the list itself is showing.
    /// </summary>
    /// <remarks>
    /// A <see cref="CoverArtPickerModel"/> rather than anything of this dialog's own, so a game
    /// picked here goes through exactly what the game page goes through - same search, same
    /// disambiguation, same write. Two implementations of "choose a cover" would be two things to
    /// keep in step, and this one exists precisely because the scan could not choose.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultsVisibility))]
    [NotifyPropertyChangedFor(nameof(ReadyVisibility))]
    [NotifyPropertyChangedFor(nameof(NeedsYouVisibility))]
    [NotifyPropertyChangedFor(nameof(PickerVisibility))]
    public partial CoverArtPickerModel? ActivePicker { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PickerHeading))]
    public partial CoverScanNeedsYouItem? PickingFor { get; set; }

    /// <summary>
    /// The row the list has selected, which is how a game gets picked for.
    /// </summary>
    /// <remarks>
    /// Cleared as soon as it is acted on, so coming back to the list leaves nothing highlighted and
    /// the same row can be chosen again - a cover picked once is often picked twice.
    /// </remarks>
    [ObservableProperty]
    public partial CoverScanNeedsYouItem? SelectedNeedsYou { get; set; }

    partial void OnSelectedNeedsYouChanged(CoverScanNeedsYouItem? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedNeedsYou = null;

        OpenPickerFor(value);
    }

    void OpenPickerFor(CoverScanNeedsYouItem item)
    {
        var picker = new CoverArtPickerModel(item.Game);

        picker.Finished += (sender, args) =>
        {
            item.MarkResolved();
            CloseActivePicker();
        };

        PickingFor = item;
        ActivePicker = picker;
    }

    /// <summary>Leaves the picker without writing, so the list can be worked down.</summary>
    [RelayCommand]
    void BackToList()
    {
        CloseActivePicker();
    }

    void CloseActivePicker()
    {
        ActivePicker?.Cancel();
        ActivePicker = null;
        PickingFor = null;
    }

    public CoverScanModel(IReadOnlyList<Game> games)
    {
        _games = games;
    }

    bool CanScan() => IsBusy == false;

    [RelayCommand(CanExecute = nameof(CanScan))]
    async Task ScanAsync()
    {
        var token = Restart();

        Ready.Clear();
        NeedsYou.Clear();
        HasApplied = false;
        _applied.Clear();

        IsBusy = true;
        IsStopping = false;
        CanBeStopped = true;
        StatusText = string.Empty;
        ProgressValue = 0;
        ProgressMaximum = Math.Max(1, _games.Count);

        try
        {
            var progress = new Progress<CoverScanProgress>(x =>
            {
                ProgressText = ResourceHelper.GetFormattedResourceTemplate("CoverScan_ScanningTemplate", x.Done, x.Total);
                ProgressValue = x.Done;
                ProgressMaximum = Math.Max(1, x.Total);
            });

            var result = await CoverScanRunner.ScanAsync(_games, progress, token).ConfigureAwait(true);

            foreach (var entry in result.Entries)
            {
                if (entry.Outcome == CoverScanOutcome.Ready)
                {
                    var item = new CoverScanReadyItem(entry);

                    // Through RefreshCounts, so the label and the button's enablement can only ever
                    // come from the same call. Raising just the label meant unticking every row
                    // recomputed the text to "Apply 0 covers" while leaving the button enabled,
                    // which then ran an empty loop and answered "Applied 0 covers."
                    item.PropertyChanged += (sender, args) => RefreshCounts();

                    Ready.Add(item);
                }
                else
                {
                    NeedsYou.Add(new CoverScanNeedsYouItem(entry));
                }
            }

            HasScanned = true;

            // Said out loud when it did not get to the end. A short list after a stopped or
            // abandoned scan is otherwise indistinguishable from a library that mostly has no art,
            // and rescanning is the right next move in one of those cases and not the other.
            StatusText = result.Completion switch
            {
                CoverScanCompletion.Stopped => ResourceHelper.GetFormattedResourceTemplate("CoverScan_StoppedTemplate", result.Scanned, result.Total),
                CoverScanCompletion.GaveUp => ResourceHelper.GetFormattedResourceTemplate("CoverScan_GaveUpTemplate", CoverScanRunner.ConsecutiveFailuresBeforeGivingUp),
                _ => string.Empty,
            };
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception err)
        {
            Logger.Error(err);
            StatusText = err is SteamGridDbException ? err.Message : ResourceHelper.GetString("General_Error");
        }
        finally
        {
            IsBusy = false;
            IsStopping = false;
            CanBeStopped = false;
            ProgressText = string.Empty;
            RefreshCounts();
        }
    }

    bool CanStop() => CanBeStopped && IsStopping == false;

    /// <summary>
    /// Stops the scan, keeping whatever it has already found.
    /// </summary>
    /// <remarks>
    /// Cancels the same token the dialog's close does, and the runner treats that as an answer
    /// rather than an error - so pressing this on game 150 of 200 leaves 150 games' worth of results
    /// on screen to apply.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanStop))]
    void Stop()
    {
        IsStopping = true;
        _cancellation?.Cancel();
    }

    bool CanApply() => IsBusy == false && Ready.Any(x => x.IsSelected);

    [RelayCommand(CanExecute = nameof(CanApply))]
    async Task ApplyAsync()
    {
        var token = Restart();
        var selected = Ready.Where(x => x.IsSelected).ToList();

        IsBusy = true;
        IsStopping = false;
        CanBeStopped = true;
        StatusText = string.Empty;
        ProgressValue = 0;
        ProgressMaximum = Math.Max(1, selected.Count);

        var written = 0;
        var failed = 0;

        try
        {
            for (var index = 0; index < selected.Count; index++)
            {
                token.ThrowIfCancellationRequested();

                ProgressText = ResourceHelper.GetFormattedResourceTemplate("CoverScan_ApplyingTemplate", index + 1, selected.Count);
                ProgressValue = index + 1;

                var outcome = await CoverScanRunner.ApplyAsync(selected[index].Entry, token).ConfigureAwait(true);

                // Counted only when a cover actually reached the disk. This counted every attempt,
                // so "Applied 12 covers." could be true of none of them - and undo would have had
                // twelve games in its list that it never changed.
                if (outcome.Written == false)
                {
                    failed++;
                    continue;
                }

                _applied.Add((selected[index].Entry.Game, outcome.BackupPath));
                written++;
            }

            StatusText = written == 1
                ? ResourceHelper.GetString("CoverScan_AppliedOne")
                : ResourceHelper.GetFormattedResourceTemplate("CoverScan_AppliedTemplate", written);

            if (failed > 0)
            {
                // Said rather than left to be noticed. A batch that half worked is the case where
                // a silent count is most misleading.
                StatusText += " " + ResourceHelper.GetFormattedResourceTemplate("CoverScan_SomeFailedTemplate", failed, selected.Count);
            }

            HasApplied = written > 0;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception err)
        {
            Logger.Error(err);
            StatusText = err is SteamGridDbException ? err.Message : ResourceHelper.GetString("General_Error");

            // Whatever did get written is still undoable, which matters more after a failure
            // part way through than after a clean run.
            HasApplied = written > 0;
        }
        finally
        {
            IsBusy = false;
            IsStopping = false;
            CanBeStopped = false;
            ProgressText = string.Empty;
        }
    }

    bool CanUndo() => IsBusy == false && HasApplied;

    /// <summary>
    /// Puts back exactly what this batch replaced.
    /// </summary>
    /// <remarks>
    /// A game that had a custom cover before gets that file back; a game that did not has the file
    /// removed, which drops it to its store art. Either way it is this batch and nothing else - a
    /// cover set by hand before the scan is not touched.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    async Task UndoAsync()
    {
        IsBusy = true;

        try
        {
            foreach (var (game, backupPath) in _applied)
            {
                try
                {
                    if (backupPath is not null && File.Exists(backupPath))
                    {
                        File.Copy(backupPath, game.ExpectedCustomCoverImage, overwrite: true);
                        File.Delete(backupPath);
                    }
                    else if (File.Exists(game.ExpectedCustomCoverImage))
                    {
                        File.Delete(game.ExpectedCustomCoverImage);
                    }

                    game.CoverImage = null;
                    await game.LoadCoverImageAsync().ConfigureAwait(true);
                }
                catch (Exception err)
                {
                    Logger.Error(err, $"Could not put back the cover for {game.Title}.");
                }
            }

            _applied.Clear();
            HasApplied = false;
            StatusText = ResourceHelper.GetString("CoverScan_Undone");
        }
        finally
        {
            IsBusy = false;
        }
    }

    void RefreshCounts()
    {
        OnPropertyChanged(nameof(ReadyHeading));
        OnPropertyChanged(nameof(ReadyVisibility));
        OnPropertyChanged(nameof(NeedsYouVisibility));
        OnPropertyChanged(nameof(ApplyLabel));
        ApplyCommand.NotifyCanExecuteChanged();
    }

    CancellationToken Restart()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();

        return _cancellation.Token;
    }

    /// <summary>
    /// Stops the scan, and tidies away any backups the batch is still holding.
    /// </summary>
    /// <remarks>
    /// Called when the dialog closes. Once it is gone there is nothing left to press undo with, so
    /// keeping the copies would only leave files behind that nothing can ever use.
    /// </remarks>
    public void Close()
    {
        _cancellation?.Cancel();

        foreach (var (_, backupPath) in _applied)
        {
            if (backupPath is null)
            {
                continue;
            }

            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch (Exception err)
            {
                Logger.Error(err);
            }
        }

        _applied.Clear();
    }
}
