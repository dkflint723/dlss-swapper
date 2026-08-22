using DLSS_Swapper.Attributes;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.UserControls;

public class CoverArtPickerModelTranslationProperties : LocalizedViewModelBase
{
    [TranslationProperty]
    public string SearchLabelText => ResourceHelper.GetString("CoverArt_SearchLabel");

    [TranslationProperty]
    public string SearchText => ResourceHelper.GetString("CoverArt_Search");

    [TranslationProperty]
    public string PickGameText => ResourceHelper.GetString("CoverArt_PickGame");

    [TranslationProperty]
    public string PickArtText => ResourceHelper.GetString("CoverArt_PickArt");

    [TranslationProperty]
    public string ApplyText => ResourceHelper.GetString("CoverArt_Apply");

    [TranslationProperty]
    public string ChooseFileText => ResourceHelper.GetString("CoverArt_ChooseFile");

    [TranslationProperty]
    public string BackText => ResourceHelper.GetString("CoverArt_Back");
}
