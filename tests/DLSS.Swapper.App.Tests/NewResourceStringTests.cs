using DLSS_Swapper.Helpers;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Every resource key added for the redesign resolves.
/// </summary>
/// <remarks>
/// The resource map reports a miss as the sentinel string rather than throwing, so a key that was
/// added to the code and not to the .resw renders as "LangResourceError..." in the window and
/// nothing else notices. These are the keys this work introduced, in one place, so adding a string
/// in code without adding the string fails here rather than in front of a user.
/// </remarks>
public class NewResourceStringTests
{
    [Theory]
    [InlineData("Preview_TitleTemplate")]
    [InlineData("Preview_TitleOneGameTemplate")]
    [InlineData("Preview_TitleOneFile")]
    [InlineData("Preview_Body")]
    [InlineData("Preview_CloseGamesFirst")]
    [InlineData("Preview_ConfirmTemplate")]
    [InlineData("Preview_ConfirmOneFile")]
    [InlineData("Update_StopAfterThisOne")]
    [InlineData("Update_Stopping")]
    [InlineData("Update_ProgressTemplate")]
    [InlineData("Update_DoneTemplate")]
    [InlineData("Update_DoneOneFileTemplate")]
    [InlineData("Update_DoneNothing")]
    [InlineData("Update_DonePartialTemplate")]
    [InlineData("Update_DoneReassurance")]
    [InlineData("Update_UndoAll")]
    [InlineData("Update_SeeWhatFailed")]
    [InlineData("Update_Undoing")]
    [InlineData("Update_UndoneTemplate")]
    [InlineData("Upscalers_NotUsed")]
    [InlineData("Upscalers_UsedByOneGame")]
    [InlineData("Upscalers_UsedByGamesTemplate")]
    [InlineData("Upscalers_Downloading")]
    [InlineData("Upscalers_ShowGamesUsing")]
    [InlineData("Upscalers_Search")]
    [InlineData("Upscalers_SearchHint")]
    [InlineData("Upscalers_ClearSearch")]
    [InlineData("Upscalers_SearchSummaryTemplate")]
    [InlineData("Upscalers_NoSearchResultsTemplate")]
    [InlineData("Upscalers_SearchElsewhereTemplate")]
    [InlineData("Upscalers_SearchNowhere")]
    [InlineData("Upscalers_ShowAllTemplate")]
    [InlineData("Upscalers_NoVersionsTemplate")]
    [InlineData("Upscalers_NoVersionsBody")]
    [InlineData("Upscalers_RefreshList")]
    [InlineData("Update_SeeWhatChanged")]
    [InlineData("Update_VersionChangeTemplate")]
    [InlineData("GamesPage_ClearDllFilter")]
    [InlineData("SettingsPage_LibraryFound")]
    [InlineData("SettingsPage_LibraryNotFound")]
    [InlineData("SettingsPage_DLSSOptions_GlobalPreset_Desc")]
    [InlineData("SettingsPage_DLSSDOptions_GlobalPreset_Desc")]
    [InlineData("SettingsPage_DLSSGOptions_GlobalPreset_Desc")]
    [InlineData("SettingsPage_Networking_Desc")]
    [InlineData("SettingsPage_Logging_Desc")]
    [InlineData("SettingsPage_Language_Desc")]
    [InlineData("SettingsPage_OpenTranslationToolbox_Desc")]
    [InlineData("SettingsPage_AllowUntrusted_Desc")]
    [InlineData("SettingsPage_AllowDebugDlls_Desc")]
    [InlineData("SettingsPage_ShowOnlyDownloadedDlls_Desc")]
    [InlineData("General_Open")]
    [InlineData("Settings_Accent")]
    [InlineData("Settings_Accent_Desc")]
    [InlineData("Settings_Accent_FollowingDesktop")]
    [InlineData("Settings_Accent_BrandGreen")]
    [InlineData("Settings_Accent_WindowsBlue")]
    [InlineData("Settings_Accent_Violet")]
    [InlineData("Settings_Accent_Amber")]
    [InlineData("Settings_MatchDesktopAccent")]
    [InlineData("Settings_MatchDesktopAccent_Desc")]
    [InlineData("FirstRun_Title")]
    [InlineData("FirstRun_Body")]
    [InlineData("FirstRun_Scan")]
    [InlineData("FirstRun_ChooseFolder")]
    [InlineData("FirstRun_Duration")]
    [InlineData("GamesPage_Empty_Title")]
    [InlineData("GamesPage_Empty_BodyTemplate")]
    [InlineData("GamesPage_Empty_ShowAllTemplate")]
    [InlineData("GamesPage_NoSearchResultsTemplate")]
    [InlineData("GamesPage_ClearSearch")]
    [InlineData("About_ThisBuild")]
    [InlineData("About_BasedOn")]
    [InlineData("About_OriginalCommunity")]
    [InlineData("About_FeedbackForkInfo")]
    public void TheStringResolves(string resourceKey)
    {
        var value = ResourceHelper.GetString(resourceKey);

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.DoesNotContain("LangResourceError", value);
    }
}
