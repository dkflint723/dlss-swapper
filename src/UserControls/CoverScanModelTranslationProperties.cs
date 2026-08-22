using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.UserControls;

public class CoverScanModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string IntroText => ResourceHelper.GetString("CoverScan_Intro");

    [TranslationProperty]
    public string ScanText => ResourceHelper.GetString("CoverScan_Scan");

    [TranslationProperty]
    public string NeedsYouHeaderText => ResourceHelper.GetString("CoverScan_NeedsYouHeader");

    [TranslationProperty]
    public string NeedsYouHintText => ResourceHelper.GetString("CoverScan_NeedsYouHint");

    [TranslationProperty]
    public string UndoText => ResourceHelper.GetString("CoverScan_Undo");

    [TranslationProperty]
    public string BackToListText => ResourceHelper.GetString("CoverScan_BackToList");
}
