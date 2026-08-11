using System.Windows.Input;
using DLSS_Swapper.Data;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// The one thing a game row is offering to do, if it is offering anything.
/// </summary>
/// <remarks>
/// Separate from <see cref="GameStatusView"/> because the card floats this over the top corner of
/// the cover while the row keeps it at the end of the line, so they cannot share a parent. What
/// they must share is the wiring: the label, the glyph, whether it appears at all and which game it
/// runs against all come from the row's status. They did not, once — the button read "Save a copy"
/// and ran the update command, because the label and the command were bound separately.
/// </remarks>
public sealed partial class GameActionButton : UserControl
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(GameRowStatus), typeof(GameActionButton), new PropertyMetadata(null, OnVisualsChanged));

    public static readonly DependencyProperty GameProperty = DependencyProperty.Register(
        nameof(Game), typeof(Game), typeof(GameActionButton), new PropertyMetadata(null, OnVisualsChanged));

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(GameActionButton), new PropertyMetadata(null, OnVisualsChanged));

    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(GameStatusVariant), typeof(GameActionButton), new PropertyMetadata(GameStatusVariant.Row, OnVisualsChanged));

    /// <summary>The status this button belongs to, which decides its label and whether it shows.</summary>
    public GameRowStatus? Status
    {
        get => (GameRowStatus?)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>The game the command runs against.</summary>
    public Game? Game
    {
        get => (Game?)GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    /// <summary>The page's row action command, which works out what to do from the same status.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public GameStatusVariant Variant
    {
        get => (GameStatusVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public GameActionButton()
    {
        this.InitializeComponent();
        UpdateVisuals();
    }

    static void OnVisualsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is GameActionButton actionButton)
        {
            actionButton.UpdateVisuals();
        }
    }

    void UpdateVisuals()
    {
        var status = Status;
        var label = status?.ActionLabel ?? string.Empty;

        // A row being written has no button, but it does have something to show, so the slot stays.
        // Rows only: the card's slot is a small button on cover art, so its progress stays a ring
        // in GameStatusView.
        var isSwapping = status?.State == GameRowState.Swapping && Variant == GameStatusVariant.Row;

        // No label means the row has nothing to offer. Collapsing the control rather than the
        // button keeps it from holding a column open in the row.
        Visibility = string.IsNullOrEmpty(label) && isSwapping == false
            ? Visibility.Collapsed
            : Visibility.Visible;

        ActionButton.Visibility = string.IsNullOrEmpty(label) ? Visibility.Collapsed : Visibility.Visible;
        ActionProgress.Visibility = isSwapping ? Visibility.Visible : Visibility.Collapsed;

        // Stopped as well as hidden. An indeterminate bar left running off screen keeps animating,
        // and there is one of these per row.
        ActionProgress.IsIndeterminate = isSwapping;

        ActionButton.Command = Command;
        ActionButton.CommandParameter = Game;

        if (Variant == GameStatusVariant.Card)
        {
            ActionButton.Content = new FontIcon()
            {
                Glyph = GlyphFor(status?.State),
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.White),
            };
            ActionButton.Height = double.NaN;
            ActionButton.Padding = new Thickness(6);
            ActionButton.BorderThickness = new Thickness(0);

            // Built here rather than pulled from the theme: the button sits on cover art, which is
            // any colour at all, so it needs its own scrim regardless of light or dark.
            ActionButton.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0x00, 0x00));

            ToolTipService.SetToolTip(ActionButton, label);
        }
        else
        {
            ActionButton.Content = label;
        }
    }

    /// <summary>
    /// The card has no room for the label, so the glyph has to carry the same meaning. It showed
    /// the update arrow for every state before this, including the one whose button saves a copy.
    /// </summary>
    static string GlyphFor(GameRowState? state) => state switch
    {
        GameRowState.NoBackup => "\uE8F7",
        _ => "\uE896",
    };
}
