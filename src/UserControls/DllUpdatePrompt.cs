using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// The confirm, progress and summary dialogs around a run that writes dlls.
/// </summary>
/// <remarks>
/// The update path moved to the games page's preview sheet, so today this fronts the revert run -
/// but it stays parameterised rather than hardcoding revert words, because what it does is generic
/// and what it says must match whoever calls it. It used to hardcode "Update" and "Updating dlls",
/// which meant confirming a restore by pressing a button labelled Update and then watching a dialog
/// claim to be updating while it put the originals back.
/// </remarks>
internal static class DllUpdatePrompt
{
    /// <summary>
    /// Confirms, runs, and reports. Does nothing if there is nothing to do.
    /// </summary>
    /// <param name="title">Dialog title, so updating and reverting read differently.</param>
    /// <param name="affectedDllCount">How many dlls the operation will touch. Zero shows the nothing-to-do message.</param>
    /// <param name="confirmationMessage">Says what is about to happen, including the counts.</param>
    /// <param name="confirmButtonText">The verb on the confirm button. It has to name the operation.</param>
    /// <param name="progressTitle">What the progress dialog says is happening.</param>
    /// <param name="operation">Either the update or the revert run.</param>
    /// <param name="detailLines">One line per dll: what it is and what it becomes. Scrolls when long.</param>
    internal static async Task RunAsync(
        XamlRoot xamlRoot,
        IReadOnlyList<Game> games,
        string title,
        int affectedDllCount,
        string confirmationMessage,
        string confirmButtonText,
        string progressTitle,
        string nothingToDoMessage,
        Func<IReadOnlyList<Game>, IProgress<DllUpdateProgress>, CancellationToken, Task<DllUpdateResult>> operation,
        string summaryTemplateResourceKey,
        IReadOnlyList<string>? detailLines = null)
    {
        if (affectedDllCount == 0)
        {
            var nothingToDoDialog = new EasyContentDialog(xamlRoot)
            {
                Title = title,
                CloseButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Close,
                Content = nothingToDoMessage,
            };
            await nothingToDoDialog.ShowAsync();
            return;
        }

        // Bulk writing into game folders is worth asking about first.
        var confirmDialog = new EasyContentDialog(xamlRoot)
        {
            Title = title,
            PrimaryButtonText = confirmButtonText,
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = BuildConfirmContent(confirmationMessage, detailLines),
        };

        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await RunConfirmedAsync(xamlRoot, games, title, progressTitle, operation, summaryTemplateResourceKey);
    }

    /// <summary>
    /// The confirmation's body: the sentence, and under it the per-dll rows when the caller has
    /// them.
    /// </summary>
    /// <remarks>
    /// The rows scroll on their own rather than riding in the message string, because a library
    /// wide restore lists dozens of lines and a ContentDialog clips overflow rather than scrolling
    /// it. The sentence stays put while the rows scroll, so the question being asked is never the
    /// part that is off screen.
    /// </remarks>
    internal static object BuildConfirmContent(string confirmationMessage, IReadOnlyList<string>? detailLines)
    {
        if (detailLines is null || detailLines.Count == 0)
        {
            return confirmationMessage;
        }

        return new StackPanel()
        {
            Spacing = 12,
            Children =
            {
                new TextBlock() { Text = confirmationMessage, TextWrapping = TextWrapping.Wrap },
                new ScrollViewer()
                {
                    MaxHeight = 320,
                    Content = new TextBlock()
                    {
                        Text = string.Join(Environment.NewLine, detailLines),
                        TextWrapping = TextWrapping.Wrap,
                        Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style,
                    },
                },
            },
        };
    }

    /// <summary>
    /// Runs and reports, having been confirmed somewhere else.
    /// </summary>
    /// <remarks>
    /// Split out for the preview sheet, which is itself the confirmation and a far better one: it
    /// names every file. Asking again afterwards would be asking the same question twice.
    /// </remarks>
    internal static async Task RunConfirmedAsync(
        XamlRoot xamlRoot,
        IReadOnlyList<Game> games,
        string title,
        string progressTitle,
        Func<IReadOnlyList<Game>, IProgress<DllUpdateProgress>, CancellationToken, Task<DllUpdateResult>> operation,
        string summaryTemplateResourceKey)
    {
        var progressRun = new Run() { Text = string.Empty };
        var progressTextBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap };
        progressTextBlock.Inlines.Add(progressRun);

        // A run over a whole library downloads hundreds of megabytes, so it needs a way out.
        // Cancelling stops before the next dll rather than mid write, so nothing is left half done.
        using var cancellation = new CancellationTokenSource();

        var progressDialog = new EasyContentDialog(xamlRoot)
        {
            Title = progressTitle,
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            Content = new StackPanel()
            {
                Spacing = 16,
                Children =
                {
                    new ProgressBar() { IsIndeterminate = true },
                    progressTextBlock,
                },
            },
        };

        progressDialog.CloseButtonClick += (sender, args) =>
        {
            progressRun.Text = ResourceHelper.GetString("DllUpdate_Cancelling");
            cancellation.Cancel();

            // Held open so the run can finish the dll it is on rather than the dialog vanishing
            // while files are still being written.
            args.Cancel = true;
        };

        var progress = new Progress<DllUpdateProgress>(x => progressRun.Text = x.Description);

        _ = progressDialog.ShowAsync();

        DllUpdateResult result;
        try
        {
            result = await operation(games, progress, cancellation.Token);
        }
        finally
        {
            progressDialog.Hide();
        }

        await ShowSummaryAsync(xamlRoot, title, summaryTemplateResourceKey, result);
    }

    internal static async Task ShowSummaryAsync(XamlRoot xamlRoot, string title, string summaryTemplateResourceKey, DllUpdateResult result)
    {
        var summary = new StackPanel() { Spacing = 8 };

        // The one-variant when exactly one dll in one game moved, because "Restored 1 dlls across
        // 1 games" reads like nobody proofread the sentence a user sees after trusting the app with
        // their game folder. The convention matches the cover scan's AppliedOne pair.
        var summaryText = result.Swapped == 1 && result.GamesUpdated == 1
            ? ResourceHelper.GetString(summaryTemplateResourceKey + "One")
            : ResourceHelper.GetFormattedResourceTemplate(summaryTemplateResourceKey, result.Swapped, result.GamesUpdated);

        summary.Children.Add(new TextBlock()
        {
            Text = summaryText,
            TextWrapping = TextWrapping.Wrap,
        });

        if (result.Failures.Count > 0)
        {
            summary.Children.Add(new TextBlock()
            {
                Text = ResourceHelper.GetString("DllUpdate_FailuresHeader"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
            });

            // Listed rather than summarised, because "3 failed" does not tell you which games to
            // close or which need running as administrator.
            summary.Children.Add(new TextBlock()
            {
                Text = string.Join(Environment.NewLine, result.Failures),
                TextWrapping = TextWrapping.Wrap,
                Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style,
            });
        }

        var summaryDialog = new EasyContentDialog(xamlRoot)
        {
            Title = title,
            CloseButtonText = ResourceHelper.GetString("General_Okay"),
            DefaultButton = ContentDialogButton.Close,
            Content = new ScrollViewer() { Content = summary, MaxHeight = 400 },
        };

        await summaryDialog.ShowAsync();
    }
}
