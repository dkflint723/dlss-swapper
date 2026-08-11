using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Pages;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the strip that reports an update run: what it says while writing, what it says
/// afterwards, and what it offers to put back. The strip is the only place the app admits a batch
/// was partly refused, so its sentences have to follow the result rather than the intent.
/// </summary>
public class UpdateBatchTests
{
    static DllUpdateProgress Progress(int currentIndex, int totalCount)
    {
        return new DllUpdateProgress()
        {
            CurrentIndex = currentIndex,
            TotalCount = totalCount,
            GameTitle = "Cyberpunk 2077",
            EngineName = "FSR 3.1 DirectX 12",
        };
    }

    static DllUpdateResult ResultWith(int succeeded, params string[] failures)
    {
        var result = new DllUpdateResult();

        for (var i = 0; i < succeeded; i++)
        {
            result.Succeeded.Add(new DllWorkItem(new TestGame($"batch_{i}") { Title = $"Game {i}" }, GameAssetType.DLSS));
        }

        result.Failures.AddRange(failures);
        return result;
    }

    static DllUpdateResult ResultWithChanges(int succeeded, params string[] failures)
    {
        var result = ResultWith(succeeded, failures);

        for (var i = 0; i < succeeded; i++)
        {
            result.Changes.Add(new DllChange()
            {
                GameTitle = $"Game {i}",
                EngineName = "DLSS",
                FromVersion = "3.7.20",
                ToVersion = "310.7",
            });
        }

        return result;
    }

    [Fact]
    public void ADoneBatchCanSayWhatEachFileBecame()
    {
        var batch = new UpdateBatchModel();

        batch.Complete(ResultWithChanges(2));

        Assert.True(batch.HasChanges);
        Assert.Equal(2, batch.Changes.Count);

        // The version it came from is the part the strip could never say, and the part worth
        // checking before deciding to keep the batch.
        var change = batch.Changes[0];
        Assert.Contains("3.7.20", change.VersionChange);
        Assert.Contains("310.7", change.VersionChange);
        Assert.Contains("Game 0", change.Description);
        Assert.Contains("DLSS", change.Description);
    }

    [Fact]
    public void AFileWithNothingBeforeItJustSaysWhatItIsNow()
    {
        var change = new DllChange()
        {
            GameTitle = "Cyberpunk 2077",
            EngineName = "DLSS",
            FromVersion = string.Empty,
            ToVersion = "310.7",
        };

        // An arrow from nothing reads as a missing value rather than as a new file.
        Assert.Equal("310.7", change.VersionChange);
    }

    [Fact]
    public void FailuresAreOfferedBeforeChanges()
    {
        // Both buttons are the same slot on the strip. A batch that half worked has changes as well
        // as failures, and drawing both would put one on top of the other.
        var partial = new UpdateBatchModel();
        partial.Complete(ResultWithChanges(2, "Could not replace nvngx_dlss.dll"));

        Assert.True(partial.HasFailures);
        Assert.True(partial.HasChanges);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, partial.FailuresVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, partial.ChangesVisibility);

        var clean = new UpdateBatchModel();
        clean.Complete(ResultWithChanges(2));

        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, clean.FailuresVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, clean.ChangesVisibility);
    }

    [Fact]
    public void UndoingABatchLeavesNothingToShow()
    {
        var batch = new UpdateBatchModel();
        batch.Complete(ResultWithChanges(2));

        batch.CompleteUndo(ResultWith(2));

        // The batch it described has just been put back, so there is nothing left for it to be
        // about. Leaving the list up would offer to show changes that are no longer on disk.
        Assert.False(batch.HasChanges);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, batch.ChangesVisibility);
    }

    [Fact]
    public void WhileRunningItSaysHowFarThroughItIs()
    {
        var batch = new UpdateBatchModel();

        batch.Report(Progress(3, 7));

        Assert.True(batch.IsRunning);
        Assert.False(batch.IsDone);
        Assert.Contains("3", batch.ProgressText);
        Assert.Contains("7", batch.ProgressText);
        Assert.Contains("Cyberpunk 2077", batch.CurrentItemText);
        Assert.Contains("FSR 3.1 DirectX 12", batch.CurrentItemText);
    }

    [Fact]
    public void TheBarNeverRunsAheadOfTheFileBeingWritten()
    {
        var batch = new UpdateBatchModel();

        // Starting the first of seven is no progress yet, not one seventh of the way done: the
        // file has not been written when its name goes up.
        batch.Report(Progress(1, 7));
        Assert.Equal(0, batch.ProgressValue);

        batch.Report(Progress(7, 7));
        Assert.True(batch.ProgressValue < 100);
    }

    [Fact]
    public void ADoneBatchOffersToPutItselfBack()
    {
        var batch = new UpdateBatchModel();

        batch.Complete(ResultWith(succeeded: 3));

        Assert.True(batch.IsDone);
        Assert.True(batch.CanUndo);
        Assert.Equal(3, batch.WrittenItems.Count);
        Assert.False(batch.HasFailures);
        Assert.False(string.IsNullOrWhiteSpace(batch.DoneDetailText));
    }

    [Fact]
    public void ABatchThatWroteNothingOffersNoUndo()
    {
        var batch = new UpdateBatchModel();

        batch.Complete(ResultWith(succeeded: 0));

        Assert.True(batch.IsDone);
        Assert.False(batch.CanUndo);

        // Nothing was written, so there is nothing to reassure anyone about.
        Assert.Empty(batch.DoneDetailText);
    }

    [Fact]
    public void APartlyRefusedBatchSaysSoAndNamesTheCount()
    {
        var batch = new UpdateBatchModel();

        batch.Complete(ResultWith(succeeded: 5, "Game A - DLSS: in use", "Game B - XeSS: in use"));

        Assert.True(batch.HasFailures);
        Assert.Equal(2, batch.Failures.Count);

        // "5 of 7 files updated · 2 could not be replaced" - a batch that half worked must not
        // report as a batch that worked.
        Assert.Contains("5", batch.DoneText);
        Assert.Contains("7", batch.DoneText);
        Assert.Contains("2", batch.DoneText);

        // What did write is still undoable.
        Assert.True(batch.CanUndo);
    }

    [Fact]
    public void FailureIsMarkedByAGlyphNotOnlyByWording()
    {
        var succeeded = new UpdateBatchModel();
        succeeded.Complete(ResultWith(succeeded: 2));

        var partial = new UpdateBatchModel();
        partial.Complete(ResultWith(succeeded: 1, "Game A - DLSS: in use"));

        // The two outcomes are told apart by the glyph as well as the sentence, never by colour.
        Assert.NotEqual(succeeded.DoneGlyph, partial.DoneGlyph);
        Assert.False(string.IsNullOrEmpty(succeeded.DoneGlyph));
        Assert.False(string.IsNullOrEmpty(partial.DoneGlyph));
    }

    [Fact]
    public void OneFileIsNeverDescribedAsFiles()
    {
        var batch = new UpdateBatchModel();

        batch.Complete(ResultWith(succeeded: 1));

        Assert.DoesNotContain("1 files", batch.DoneText);
        Assert.DoesNotContain("1 games", batch.DoneText);
    }

    [Fact]
    public void UndoingLeavesNothingFurtherToUndo()
    {
        var batch = new UpdateBatchModel();
        batch.Complete(ResultWith(succeeded: 2));

        batch.CompleteUndo(ResultWith(succeeded: 2));

        Assert.True(batch.IsDone);
        Assert.False(batch.CanUndo);
        Assert.Empty(batch.WrittenItems);
    }

    [Fact]
    public void UndoOnlyEverTouchesWhatTheBatchWrote()
    {
        var game = new TestGame("batch_undo") { Title = "Only" };

        var result = new DllUpdateResult();
        result.Succeeded.Add(new DllWorkItem(game, GameAssetType.DLSS));

        var batch = new UpdateBatchModel();
        batch.Complete(result);

        // The game may well have older swaps with backups sitting beside them. Undo is scoped to
        // this batch, so it cannot quietly revert a swap made last week.
        var written = batch.WrittenItems.ToList();
        Assert.Single(written);
        Assert.Equal(GameAssetType.DLSS, written[0].AssetType);
        Assert.Same(game, written[0].Game);
    }

    [Fact]
    public void StoppingSaysItWillFinishTheFileItIsOn()
    {
        var batch = new UpdateBatchModel();

        // The promise is on the button before it is pressed, not in a message afterwards.
        Assert.True(batch.CanStop);
        Assert.False(string.IsNullOrWhiteSpace(batch.StopLabel));
    }
}
