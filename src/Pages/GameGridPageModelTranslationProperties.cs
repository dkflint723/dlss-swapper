using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Pages;

public class GameGridPageModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string NewDllsText => ResourceHelper.GetString("GamesPage_NewDlls");


    [TranslationProperty]
    public string NeverUpdateThisGameText => ResourceHelper.GetString("GamesPage_Action_TurnUpdatesOff");

    [TranslationProperty]
    public string AddGameText => ResourceHelper.GetString("GamesPage_AddGame");

    [TranslationProperty]
    public string RefreshText => ResourceHelper.GetString("General_Refresh");

    /// <summary>The View menu's grouping toggle, which used to live behind the Filter button.</summary>
    [TranslationProperty]
    public string GroupByLibraryText => ResourceHelper.GetString("GamesPage_GroupGamesFromTheSameLibraryTogether");

    [TranslationProperty]
    public string SearchText => ResourceHelper.GetString("General_Search");

    [TranslationProperty]
    public string ViewTypeText => ResourceHelper.GetString("GamesPage_ViewType");

    [TranslationProperty]
    public string GridViewText => ResourceHelper.GetString("GamesPage_ViewType_GridView");

    [TranslationProperty]
    public string ListViewText => ResourceHelper.GetString("GamesPage_ViewType_ListView");

    [TranslationProperty]
    public string PageTitle => ResourceHelper.GetString("GamesPage_Title");

    [TranslationProperty]
    public string ClearDllFilterText => ResourceHelper.GetString("GamesPage_ClearDllFilter");

    [TranslationProperty]
    public string ApplicationRunsInAdministrativeModeInfo => ResourceHelper.GetString("General_ApplicationRunningAsAdmin");

    [TranslationProperty]
    public string FindCoversText => ResourceHelper.GetString("GamesPage_FindCovers");

    /// <summary>
    /// The admin warning bar's title. The identical bar on the other two pages binds this; only
    /// this one had the word typed into the markup, where no translator could see it.
    /// </summary>
    [TranslationProperty]
    public string WarningTitleText => ResourceHelper.GetString("General_Warning_Title");

    [TranslationProperty]
    public string ClearSearchText => ResourceHelper.GetString("GamesPage_ClearSearch");
}
