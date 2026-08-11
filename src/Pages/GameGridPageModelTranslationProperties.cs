using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Pages;

public class GameGridPageModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string NewDllsText => ResourceHelper.GetString("GamesPage_NewDlls");

    [TranslationProperty]
    public string UpdateAllGamesText => ResourceHelper.GetString("DllUpdate_UpdateAllGames");

    [TranslationProperty]
    public string NeverUpdateThisGameText => ResourceHelper.GetString("GamesPage_Action_TurnUpdatesOff");

    [TranslationProperty]
    public string AddGameText => ResourceHelper.GetString("GamesPage_AddGame");

    [TranslationProperty]
    public string RefreshText => ResourceHelper.GetString("General_Refresh");

    [TranslationProperty]
    public string FilterText => ResourceHelper.GetString("General_Filter");

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
}
