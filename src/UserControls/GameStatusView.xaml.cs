using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

/// <summary>Which surface a status is being drawn on.</summary>
public enum GameStatusVariant
{
    /// <summary>A list row, on the page background.</summary>
    Row,

    /// <summary>A grid card, over cover art.</summary>
    Card,
}

/// <summary>
/// What one game is currently saying: a glyph, a sentence and the engines it ships.
/// </summary>
/// <remarks>
/// One control rather than the same markup in the list row and the grid card. Those two templates
/// drifted apart every time either was touched — the card was still announcing "DLSS" and a version
/// number long after the row had moved to sentences — because nothing forced them to agree. They
/// share meaning, not layout, so the call sites still place this where they want it; only the paint
/// differs, and that is a visual state rather than a second copy of the markup.
/// </remarks>
public sealed partial class GameStatusView : UserControl
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(GameRowStatus), typeof(GameStatusView), new PropertyMetadata(null, OnVisualsChanged));

    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(GameStatusVariant), typeof(GameStatusView), new PropertyMetadata(GameStatusVariant.Row, OnVisualsChanged));

    /// <summary>The game's current status, or null before it has been worked out.</summary>
    public GameRowStatus? Status
    {
        get => (GameRowStatus?)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public GameStatusVariant Variant
    {
        get => (GameStatusVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public GameStatusView()
    {
        this.InitializeComponent();
        UpdateVisuals();
    }

    static void OnVisualsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is GameStatusView statusView)
        {
            statusView.UpdateVisuals();
        }
    }

    void UpdateVisuals()
    {
        var status = Status;

        StatusGlyph.Glyph = status?.Glyph ?? string.Empty;

        // Up to date carries no glyph at all, so the icon has to go rather than render as a blank
        // cell the sentence is then indented past.
        StatusGlyph.Visibility = string.IsNullOrEmpty(StatusGlyph.Glyph)
            ? Visibility.Collapsed
            : Visibility.Visible;

        StatusSentence.Text = status?.Sentence ?? string.Empty;
        StatusEngines.Text = status?.Engines ?? string.Empty;

        // While a swap is running the engine list is about to be wrong, so the spinner takes its
        // place. The list rows used to show the stale list here and only the card swapped it out.
        var isBusy = status?.State == GameRowState.Swapping;
        StatusEngines.Visibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
        BusyRing.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        BusyRing.IsActive = isBusy;

        VisualStateManager.GoToState(this, Variant == GameStatusVariant.Card ? "CardVariant" : "RowVariant", false);
    }
}
