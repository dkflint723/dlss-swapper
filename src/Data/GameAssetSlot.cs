using CommunityToolkit.Mvvm.ComponentModel;

namespace DLSS_Swapper.Data;

/// <summary>
/// What a game currently has installed for one dll type.
/// </summary>
/// <remarks>
/// Replaces the pair of properties Game used to carry per type, CurrentDLSS and MultipleDLSSFound
/// and so on for all nine. Holding it as an object per type means the view can be given a type and
/// look up its own slot, rather than every type needing its own named property to bind against.
/// </remarks>
public partial class GameAssetSlot : ObservableObject
{
    public required GameAssetType AssetType { get; init; }

    /// <summary>The dll installed for this type, or null when the game does not have one.</summary>
    [ObservableProperty]
    public partial GameAsset? CurrentAsset { get; set; } = null;

    /// <summary>True when the game keeps this dll in more than one place.</summary>
    [ObservableProperty]
    public partial bool MultipleFound { get; set; } = false;
}
