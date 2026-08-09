using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.UserControls;

public class ShellSidebarModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string GamesText => ResourceHelper.GetString("GamesPage_Title");

    [TranslationProperty]
    public string UpscalersText => ResourceHelper.GetString("Sidebar_Upscalers");

    [TranslationProperty]
    public string SettingsText => ResourceHelper.GetString("SettingsPage_Title");

    [TranslationProperty]
    public string BackupCoverageLabel => ResourceHelper.GetString("Sidebar_BackupCoverageLabel");
}
