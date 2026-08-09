using System.Linq;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// The name of a dll type and which version the game currently has, with a dropdown to change it.
/// </summary>
/// <remarks>
/// This block used to be written out once per dll type in GameControl, with the type hardcoded in
/// five places each time. Giving the control the type instead means the markup is written once, and
/// the control finds the game's slot for that type itself.
/// </remarks>
public sealed partial class GameAssetPicker : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(GameControlModel),
        typeof(GameAssetPicker),
        new PropertyMetadata(null, OnInputChanged));

    public GameControlModel? ViewModel
    {
        get => (GameControlModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty AssetTypeProperty = DependencyProperty.Register(
        nameof(AssetType),
        typeof(GameAssetType),
        typeof(GameAssetPicker),
        new PropertyMetadata(GameAssetType.Unknown, OnInputChanged));

    public GameAssetType AssetType
    {
        get => (GameAssetType)GetValue(AssetTypeProperty);
        set => SetValue(AssetTypeProperty, value);
    }

    /// <summary>
    /// The game's slot for <see cref="AssetType"/>.
    /// </summary>
    /// <remarks>
    /// A dependency property rather than a plain getter so the bindings below update when the view
    /// model or the type arrives. Slots themselves are never replaced, so once this is set it stays
    /// pointed at the same object for the life of the game.
    /// </remarks>
    public static readonly DependencyProperty AssetSlotProperty = DependencyProperty.Register(
        nameof(AssetSlot),
        typeof(GameAssetSlot),
        typeof(GameAssetPicker),
        new PropertyMetadata(null));

    public GameAssetSlot? AssetSlot
    {
        get => (GameAssetSlot?)GetValue(AssetSlotProperty);
        private set => SetValue(AssetSlotProperty, value);
    }

    public static readonly DependencyProperty TypeNameProperty = DependencyProperty.Register(
        nameof(TypeName),
        typeof(string),
        typeof(GameAssetPicker),
        new PropertyMetadata(string.Empty));

    public string TypeName
    {
        get => (string)GetValue(TypeNameProperty);
        private set => SetValue(TypeNameProperty, value);
    }

    public GameAssetPicker()
    {
        InitializeComponent();
    }

    static void OnInputChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not GameAssetPicker gameAssetPicker)
        {
            return;
        }

        gameAssetPicker.AssetSlot = gameAssetPicker.ViewModel?.Game?.GetAssetSlot(gameAssetPicker.AssetType);

        gameAssetPicker.TypeName = gameAssetPicker.AssetType == GameAssetType.Unknown
            ? string.Empty
            : DLLManager.Instance.GetAssetTypeName(gameAssetPicker.AssetType);

        gameAssetPicker.UpdatePresence();
    }

    /// <summary>
    /// Hides itself when the game does not ship this dll.
    /// </summary>
    /// <remarks>
    /// All nine pickers are declared in the game view whatever the game has, so most of them used
    /// to read "Not found". A game with only XeSS showed eight controls saying nothing and one
    /// saying something. The absent types are still worth naming, but as one line rather than as
    /// eight controls, which is what the summary beneath them is for.
    /// </remarks>
    void UpdatePresence()
    {
        var game = ViewModel?.Game;

        var hasThisDll = game is not null
            && game.GameAssets.Any(x => x.AssetType == AssetType);

        Visibility = hasThisDll ? Visibility.Visible : Visibility.Collapsed;
    }
}
