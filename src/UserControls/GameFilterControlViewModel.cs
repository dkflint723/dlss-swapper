using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;

namespace DLSS_Swapper.UserControls;

public partial class GameFilterControlViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool ShowHiddenGames { get; set; } = false;

    [ObservableProperty]
    public partial bool GroupGameLibrariesTogether { get; set; } = false;
    
    public GameFilterControlViewModelTranslationProperties TranslationProperties { get; } = new GameFilterControlViewModelTranslationProperties();

    public GameFilterControlViewModel()
    {
        ShowHiddenGames = GameManager.Instance.ShowHiddenGames;
        GroupGameLibrariesTogether = Settings.Instance.GroupGameLibrariesTogether;
    }
}
