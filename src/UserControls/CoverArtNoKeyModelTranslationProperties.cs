using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.UserControls;

public class CoverArtNoKeyModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string ExplanationText => ResourceHelper.GetString("CoverArt_NoApiKey");

    [TranslationProperty]
    public string HowToText => ResourceHelper.GetString("SettingsPage_SteamGridDbKeyHowTo");

    [TranslationProperty]
    public string GetKeyText => ResourceHelper.GetString("SettingsPage_SteamGridDbGetKey");

    [TranslationProperty]
    public string PasteHereText => ResourceHelper.GetString("CoverArt_PasteKeyHere");

    [TranslationProperty]
    public string PlaceholderText => ResourceHelper.GetString("SettingsPage_SteamGridDbKeyPlaceholder");

    [TranslationProperty]
    public string SaveText => ResourceHelper.GetString("CoverArt_SaveKey");
}
