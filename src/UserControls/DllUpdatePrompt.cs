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
/// The confirm, progress and summary dialogs around an update run.
/// </summary>
/// <remarks>
/// Shared so updating one game and updating every game behave the same. The only difference between
/// them is which games go in and what the confirmation says.
/// </remarks>
internal static class DllUpdatePrompt
{
    /// <summary>
    /// Confirms, runs, and reports. Does nothing if there is nothing to do.
    /// </summary>
    /// <param name="title">Dialog title, so updating and reverting read differently.</param>
    /// <param name="affectedDllCount">How many dlls the operation will touch. Zero shows the nothing-to-do message.</param>
    /// <param name="confirmationMessage">Says what is about to happen, including the counts.</param>
    /// <param name="operation">Either the update or the revert run.</param>
    internal static async Task RunAsync(
        XamlRoot xamlRoot,
        IReadOnlyList<Game> games,
        string title,
        int affectedDllCount,
        string confirmationMessage,
        string nothingToDoMessage,
        Func<IReadOnlyList<Game>, IProgress<DllUpdateProgress>, CancellationToken, Task<DllUpdateResult>> operation,
        string summaryTemplateResourceKey)
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
            PrimaryButtonText = ResourceHelper.GetString("General_Update"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = confirmationMessage,
        };

        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await RunConfirmedAsync(xamlRoot, games, title, operation, summaryTemplateResourceKey);
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
            Title = ResourceHelper.GetString("DllUpdate_Updating"),
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

    static async Task ShowSummaryAsync(XamlRoot xamlRoot, string title, string summaryTemplateResourceKey, DllUpdateResult result)
    {
        var summary = new StackPanel() { Spacing = 8 };

        summary.Children.Add(new TextBlock()
        {
            Text = ResourceHelper.GetFormattedResourceTemplate(summaryTemplateResourceKey, result.Swapped, result.GamesUpdated),
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
