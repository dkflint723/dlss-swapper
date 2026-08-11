using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Pages;

public class LibraryPageModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string ApplicationRunsInAdministrativeModeInfo => ResourceHelper.GetString("General_ApplicationRunningAsAdmin");

    [TranslationProperty]
    public string ImportText => ResourceHelper.GetString("General_Import");

    [TranslationProperty]
    public string ExportAllText => ResourceHelper.GetString("General_ExportAll");

    [TranslationProperty]
    public string DownloadLatestText => ResourceHelper.GetString("LibraryPage_DownloadLatest");

    [TranslationProperty]
    public string RefreshText => ResourceHelper.GetString("General_Refresh");

    [TranslationProperty]
    public string WarningText => ResourceHelper.GetString("General_Warning");

    [TranslationProperty]
    public string CancelText => ResourceHelper.GetString("General_Cancel");

    /// <summary>
    /// The same word the sidebar uses.
    /// </summary>
    /// <remarks>
    /// The rail said Upscalers and the page it opened said Library, which is two names for one
    /// place. "Upscalers" is the one that says what is in it.
    /// </remarks>
    [TranslationProperty]
    public string PageTitle => ResourceHelper.GetString("Sidebar_Upscalers");

    [TranslationProperty]
    public string SearchText => ResourceHelper.GetString("Upscalers_Search");

    /// <summary>Names what is searched, for the screen reader and for anyone who wonders.</summary>
    [TranslationProperty]
    public string SearchHintText => ResourceHelper.GetString("Upscalers_SearchHint");

    [TranslationProperty]
    public string ClearSearchText => ResourceHelper.GetString("Upscalers_ClearSearch");

    [TranslationProperty]
    public string ImportFromLocalFilesText => ResourceHelper.GetString("LibraryPage_ImportFrom_LocalFiles");

    [TranslationProperty]
    public string ImportFromDriverText => ResourceHelper.GetString("LibraryPage_ImportFrom_Driver");

    [TranslationProperty]
    public string ImportFromDownloadServerText => ResourceHelper.GetString("LibraryPage_ImportFrom_DownloadFromServer");

}
