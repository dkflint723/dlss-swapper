using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.UserControls;

/// <summary>Which page the shell is showing.</summary>
/// <remarks>
/// There is no Downloads section. Downloads are sequential and take seconds, so a page for them
/// would be empty almost every time it was opened; progress belongs on the row being downloaded and
/// in the operation strip instead.
/// </remarks>
public enum ShellSection
{
    Games,
    Upscalers,
    Settings,
}

/// <summary>
/// The sidebar's state.
/// </summary>
/// <remarks>
/// Takes its data rather than its control, so it can be built and asserted on without a window. The
/// counts and the backup sentence are the parts worth testing: they are read at a glance and a
/// wrong one is not obviously wrong.
/// </remarks>
public partial class ShellSidebarModel : ObservableObject
{
    readonly Func<IReadOnlyList<Game>> _gamesSource;
    readonly Func<int> _upscalerCountSource;

    public ShellSidebarModelTranslationProperties TranslationProperties { get; } = new ShellSidebarModelTranslationProperties();

    [ObservableProperty]
    public partial ShellSection ActiveSection { get; set; } = ShellSection.Games;

    /// <summary>Reads as "42 games", under the app name.</summary>
    [ObservableProperty]
    public partial string GameCountText { get; set; } = string.Empty;

    /// <summary>Trailing count on the Upscalers item: how many dlls are known about.</summary>
    [ObservableProperty]
    public partial string UpscalerCountText { get; set; } = string.Empty;

    /// <summary>Reads as "39 of 42 games", under a static label.</summary>
    [ObservableProperty]
    public partial string BackupCoverageText { get; set; } = string.Empty;

    /// <summary>Reads as "Fix the other 3". Hidden when every game has a copy.</summary>
    [ObservableProperty]
    public partial string FixTheOthersText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility FixTheOthersVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string VersionText { get; set; } = string.Empty;

    public ShellSidebarModel()
        : this(
            () => GameManager.Instance.GetSynchronisedGamesListCopy(),
            CountKnownDlls)
    {
    }

    internal ShellSidebarModel(Func<IReadOnlyList<Game>> gamesSource, Func<int> upscalerCountSource)
    {
        _gamesSource = gamesSource;
        _upscalerCountSource = upscalerCountSource;
        Refresh();
    }

    static int CountKnownDlls()
    {
        var count = 0;
        foreach (var dllTypeDefinition in Dlls.DllTypes.All)
        {
            count += DLLManager.Instance.GetRecords(dllTypeDefinition.AssetType)?.Count ?? 0;
        }

        return count;
    }

    /// <summary>
    /// Recomputes the counts. Called when games or dlls change, since these are snapshots.
    /// </summary>
    public void Refresh()
    {
        var summary = LibrarySummary.FromGames(_gamesSource());

        GameCountText = ResourceHelper.GetFormattedResourceTemplate("Sidebar_GameCountTemplate", summary.TotalGames);
        UpscalerCountText = _upscalerCountSource().ToString(System.Globalization.CultureInfo.CurrentCulture);

        var withBackup = summary.TotalGames - summary.GamesMissingBackups;
        BackupCoverageText = ResourceHelper.GetFormattedResourceTemplate(
            "Sidebar_BackupCoverageCountTemplate",
            withBackup,
            summary.TotalGames);

        FixTheOthersText = ResourceHelper.GetFormattedResourceTemplate(
            "Sidebar_FixTheOthersTemplate",
            summary.GamesMissingBackups);

        // Nothing to fix, so no link. The sentence above already says everything is covered.
        FixTheOthersVisibility = summary.GamesMissingBackups > 0 ? Visibility.Visible : Visibility.Collapsed;

        VersionText = ResourceHelper.GetFormattedResourceTemplate(
            "Sidebar_VersionTemplate",
            App.CurrentApp?.GetVersionString() ?? string.Empty);
    }
}
