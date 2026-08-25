using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.UserControls;

public class NewDLLsControlModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string WhileLoadingYourGameNewDllsDiscoveredHelpUsText => ResourceHelper.GetString("GamesPage_NewDlls_DiscoveredHelpUs");

    [TranslationProperty]
    public string StepOneCreateNewIssueText => ResourceHelper.GetString("GamesPage_NewDlls_StepOneCreateNewIssue");

    [TranslationProperty]
    public string CreateNewGitHubIssueText => ResourceHelper.GetString("GamesPage_NewDlls_CreateNewGitHubIssue");

    [TranslationProperty]
    public string GitHubAccountRequiredText => ResourceHelper.GetString("GamesPage_NewDlls_GitHubAccountRequired");

    [TranslationProperty]
    public string IfButtonDoesntWorkTryHereText => ResourceHelper.GetString("GamesPage_NewDlls_IfButtonDoesntWorkTryHere");

    [TranslationProperty]
    public string StepTwoCopyTitleText => ResourceHelper.GetString("GamesPage_NewDlls_StepTwoCopyTitle");

    [TranslationProperty]
    public string CopyText => ResourceHelper.GetString("General_Copy");

    [TranslationProperty]
    public string StepThreeCopyBodyText => ResourceHelper.GetString("GamesPage_NewDlls_StepThreeCopyBody");

    [TranslationProperty]
    public string StepFourSubmitYourIssueText => ResourceHelper.GetString("GamesPage_NewDlls_StepFourSubmitYourIssue");

    [TranslationProperty]
    public string YouDoNotHaveToSubmitDllAutoTrackText => ResourceHelper.GetString("GamesPage_NewDlls_YouDoNotHaveToSubmitDllAutoTrack");

    [TranslationProperty]
    public string ThanksForHelpingText => ResourceHelper.GetString("GamesPage_NewDlls_ThanksForHelping");

    /// <summary>
    /// Distinct from the body's, because both buttons shared one generic "Copy" and a screen reader
    /// met two controls with the same name and no way to tell which copied what.
    /// </summary>
    [TranslationProperty]
    public string CopyTitleActionText => ResourceHelper.GetString("NewDlls_CopyTitleAction");

    [TranslationProperty]
    public string CopyBodyActionText => ResourceHelper.GetString("NewDlls_CopyBodyAction");
}
