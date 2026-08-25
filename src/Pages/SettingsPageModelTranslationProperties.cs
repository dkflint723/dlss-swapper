using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Pages;

public class SettingsPageModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string VersionText => $"{ResourceHelper.GetString("General_Version")}:";

    [TranslationProperty]
    public string BuildDateText => $"{ResourceHelper.GetString("SettingsPage_BuildDate")}:";

    [TranslationProperty]
    public string BuildCommitText => $"{ResourceHelper.GetString("SettingsPage_BuildCommit")}:";

    [TranslationProperty]
    public string CopyText => ResourceHelper.GetString("General_Copy");

    [TranslationProperty]
    public string GiveFeedbackInfo => ResourceHelper.GetString("SettingsPage_GiveFeedbackInfo");

    [TranslationProperty]
    public string NetworkTesterText => ResourceHelper.GetString("SettingsPage_OpenNetworkTester");

    [TranslationProperty]
    public string GeneralTroubleshootingGuideText => ResourceHelper.GetString("SettingsPage_GeneralTroubleshootingGuide");

    [TranslationProperty]
    public string DiagnosticsText => ResourceHelper.GetString("SettingsPage_OpenDiagnostics");

    [TranslationProperty]
    public string AcknowledgementsText => ResourceHelper.GetString("SettingsPage_OpenAcknowledgements");

    [TranslationProperty]
    public string HideNonDLSSGamesText => ResourceHelper.GetString("SettingsPage_HideNonDLSSGames");

    [TranslationProperty]
    public string HideNonDLSSGamesInfo => ResourceHelper.GetString("SettingsPage_HideNonDLSSGamesInfo");

    [TranslationProperty]
    public string BackupNewGamesText => ResourceHelper.GetString("SettingsPage_BackupNewGames");

    [TranslationProperty]
    public string BackupNewGamesInfo => ResourceHelper.GetString("SettingsPage_BackupNewGamesInfo");

    [TranslationProperty]
    public string AllowDebugDllsInfo => ResourceHelper.GetString("SettingsPage_AllowDebugDlls_Desc");

    [TranslationProperty]
    public string AllowUntrustedInfo => ResourceHelper.GetString("SettingsPage_AllowUntrusted_Desc");

    [TranslationProperty]
    public string ApplicationRunsInAdministrativeModeInfo => ResourceHelper.GetString("General_ApplicationRunningAsAdmin");

    [TranslationProperty]
    public string WarningText => ResourceHelper.GetString("General_Warning");

    [TranslationProperty]
    public string YourCurrentLogfileText => ResourceHelper.GetString("SettingsPage_YourCurrentLogFile");

    [TranslationProperty]
    public string OpenTranslationToolboxText => ResourceHelper.GetString("SettingsPage_OpenTranslationToolbox");

    [TranslationProperty]
    public string OpenTranslationToolboxDescText => ResourceHelper.GetString("SettingsPage_OpenTranslationToolbox_Desc");

    /// <summary>For a button whose row already names what it opens.</summary>
    [TranslationProperty]
    public string OpenText => ResourceHelper.GetString("General_Open");

    [TranslationProperty]
    public string AppearanceText => ResourceHelper.GetString("Settings_Appearance");

    [TranslationProperty]
    public string BehaviourText => ResourceHelper.GetString("Settings_Behaviour");

    [TranslationProperty]
    public string ThemeDescriptionText => ResourceHelper.GetString("Settings_Theme_Desc");

    [TranslationProperty]
    public string AccentText => ResourceHelper.GetString("Settings_Accent");

    [TranslationProperty]
    public string MatchDesktopAccentText => ResourceHelper.GetString("Settings_MatchDesktopAccent");

    [TranslationProperty]
    public string MatchDesktopAccentDescriptionText => ResourceHelper.GetString("Settings_MatchDesktopAccent_Desc");

    [TranslationProperty]
    public string ThisBuildText => ResourceHelper.GetString("About_ThisBuild");

    [TranslationProperty]
    public string BasedOnText => ResourceHelper.GetString("About_BasedOn");

    [TranslationProperty]
    public string OriginalCommunityText => ResourceHelper.GetString("About_OriginalCommunity");

    [TranslationProperty]
    public string FeedbackForkInfoText => ResourceHelper.GetString("About_FeedbackForkInfo");

    [TranslationProperty]
    public string ThemeLightText => ResourceHelper.GetString("SettingsPage_ThemeLight");

    [TranslationProperty]
    public string ThemeDarkText => ResourceHelper.GetString("SettingsPage_ThemeDark");

    [TranslationProperty]
    public string ThemeSystemSettingDefaultText => ResourceHelper.GetString("SettingsPage_ThemeSystemSettingDefault");

    [TranslationProperty]
    public string ThemeModeText => ResourceHelper.GetString("SettingsPage_ThemeMode");

    [TranslationProperty]
    public string GameLibrariesText => ResourceHelper.GetString("SettingsPage_GameLibraries");

    [TranslationProperty]
    public string IgnoredPathsText => ResourceHelper.GetString("SettingsPage_IgnoredPaths");

    [TranslationProperty]
    public string AddIgnoredPathText => ResourceHelper.GetString("SettingsPage_AddIgnoredPath");

    [TranslationProperty]
    public string DLSSOptionsText => ResourceHelper.GetString("SettingsPage_DLSSOptions");

    [TranslationProperty]
    public string DLSSOptionsGlobalPresetText => ResourceHelper.GetString("SettingsPage_DLSSOptions_GlobalPreset");

    [TranslationProperty]
    public string DLSSDOptionsGlobalPresetText => ResourceHelper.GetString("SettingsPage_DLSSDOptions_GlobalPreset");

    [TranslationProperty]
    public string DLSSGOptionsGlobalPresetText => ResourceHelper.GetString("SettingsPage_DLSSGOptions_GlobalPreset");

    [TranslationProperty]
    public string DLSSOptionsGlobalPresetDescText => ResourceHelper.GetString("SettingsPage_DLSSOptions_GlobalPreset_Desc");

    [TranslationProperty]
    public string DLSSDOptionsGlobalPresetDescText => ResourceHelper.GetString("SettingsPage_DLSSDOptions_GlobalPreset_Desc");

    [TranslationProperty]
    public string DLSSGOptionsGlobalPresetDescText => ResourceHelper.GetString("SettingsPage_DLSSGOptions_GlobalPreset_Desc");

    [TranslationProperty]
    public string DLSSDeveloperOptionsText => ResourceHelper.GetString("SettingsPage_DLSSDeveloperOptions");

    [TranslationProperty]
    public string ShowOnScreenIndicatorText => ResourceHelper.GetString("SettingsPage_DLSSDeveloperOptions_ShowOnScreenIndicator");

    [TranslationProperty]
    public string VerboseLoggingText => ResourceHelper.GetString("SettingsPage_DLSSDeveloperOptions_VerboseLogging");

    [TranslationProperty]
    public string EnableLoggingToFileText => ResourceHelper.GetString("SettingsPage_DLSSDeveloperOptions_EnableLoggingToFile");

    [TranslationProperty]
    public string EnableLoggingToConsoleWindowText => ResourceHelper.GetString("SettingsPage_DLSSDeveloperOptions_EnableLoggingToConsoleWindow");

    [TranslationProperty]
    public string AllowUntrustedText => ResourceHelper.GetString("SettingsPage_SettingsAllowUntrusted");

    [TranslationProperty]
    public string AllowDebugDllsText => ResourceHelper.GetString("SettingsPage_AllowDebugDlls");

    [TranslationProperty]
    public string ShowOnlyDownloadedDllsText => ResourceHelper.GetString("SettingsPage_ShowOnlyDownloadedDlls");

    [TranslationProperty]
    public string ApliesOnlyToDllPickerNotLibraryText => ResourceHelper.GetString("SettingsPage_ShowOnlyDownloadedDlls_Desc");

    [TranslationProperty]
    public string CheckForUpdatesText => ResourceHelper.GetString("SettingsPage_SettingsCheckForUpdates");

    [TranslationProperty]
    public string GiveFeedbackText => ResourceHelper.GetString("SettingsPage_GiveFeedback");

    [TranslationProperty]
    public string TroubleshootingText => ResourceHelper.GetString("SettingsPage_Troubleshooting");

    [TranslationProperty]
    public string PageTitle => ResourceHelper.GetString("SettingsPage_Title");

    [TranslationProperty]
    public string LoggingText => ResourceHelper.GetString("SettingsPage_Logging");

    /// <summary>
    /// Doing double duty as the answer to "what does Verbose mean". The levels are a bare list of
    /// six one-word labels with nowhere to explain any of them: <see cref="Data.ComboBoxOption"/>
    /// carries only a label and a value, and the dropdown binds DisplayMemberPath to the label.
    /// Naming the two ends of the range in the row's own sentence costs nothing and is the only
    /// place a user is told what they are choosing between.
    /// </summary>
    [TranslationProperty]
    public string LoggingDescText => ResourceHelper.GetString("SettingsPage_Logging_Desc");

    [TranslationProperty]
    public string AboutText => ResourceHelper.GetString("SettingsPage_About");

    [TranslationProperty]
    public string YesText => ResourceHelper.GetString("General_Yes");

    [TranslationProperty]
    public string NoText => ResourceHelper.GetString("General_No");

    [TranslationProperty]
    public string LanguageText => ResourceHelper.GetString("SettingsPage_Language");

    [TranslationProperty]
    public string LanguageDescText => ResourceHelper.GetString("SettingsPage_Language_Desc");

    [TranslationProperty]
    public string DLSSPresetInfoTooltipText => ResourceHelper.GetString("GamePage_DLSSPresetInfo_Tooltip");

    [TranslationProperty]
    public string DLSSPresetInfoText => ResourceHelper.GetString("GamePage_DLSSPresetInfo");

    [TranslationProperty]
    public string NVAPIErrorTooltipText => ResourceHelper.GetString("GamePage_NVAPIError_Tooltip");

    [TranslationProperty]
    public string NetworkingText => ResourceHelper.GetString("SettingsPage_Networking");

    [TranslationProperty]
    public string NetworkingDescText => ResourceHelper.GetString("SettingsPage_Networking_Desc");

    [TranslationProperty]
    public string ProxySettingsText => ResourceHelper.GetString("SettingsPage_ProxySettings");

    [TranslationProperty]
    public string SteamGridDbKeyText => ResourceHelper.GetString("SettingsPage_SteamGridDbKey");

    /// <summary>
    /// What it does, then how to get one. Joined rather than kept apart because the instructions
    /// are the whole point of the row for anybody who does not already have a key, and a link on
    /// its own does not say what to do when the page opens.
    /// </summary>
    [TranslationProperty]
    public string SteamGridDbKeyInfo =>
        $"{ResourceHelper.GetString("SettingsPage_SteamGridDbKeyInfo")} {ResourceHelper.GetString("SettingsPage_SteamGridDbKeyHowTo")}";

    [TranslationProperty]
    public string SteamGridDbGetKeyText => ResourceHelper.GetString("SettingsPage_SteamGridDbGetKey");

    [TranslationProperty]
    public string SteamGridDbKeyPlaceholderText => ResourceHelper.GetString("SettingsPage_SteamGridDbKeyPlaceholder");

    [TranslationProperty]
    public string RemoveText => ResourceHelper.GetString("General_Remove");

    [TranslationProperty]
    public string CopyCommitText => ResourceHelper.GetString("Settings_CopyCommit");
}
