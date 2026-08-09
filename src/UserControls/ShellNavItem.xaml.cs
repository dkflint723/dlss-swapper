using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// One row in the shell sidebar.
/// </summary>
/// <remarks>
/// A control rather than a styled ListViewItem because the active marker is a bar flush to the
/// window edge, which a list item's own selection visual cannot produce without fighting its
/// template.
/// </remarks>
public sealed partial class ShellNavItem : UserControl
{
    public event EventHandler<ShellSection>? Invoked;

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(ShellNavItem), new PropertyMetadata(string.Empty, OnVisualsChanged));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ShellNavItem), new PropertyMetadata(string.Empty, OnVisualsChanged));

    public static readonly DependencyProperty TrailingCountProperty = DependencyProperty.Register(
        nameof(TrailingCount), typeof(string), typeof(ShellNavItem), new PropertyMetadata(string.Empty, OnVisualsChanged));

    public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
        nameof(Section), typeof(ShellSection), typeof(ShellNavItem), new PropertyMetadata(ShellSection.Games, OnVisualsChanged));

    public static readonly DependencyProperty ActiveSectionProperty = DependencyProperty.Register(
        nameof(ActiveSection), typeof(ShellSection), typeof(ShellNavItem), new PropertyMetadata(ShellSection.Games, OnVisualsChanged));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string TrailingCount
    {
        get => (string)GetValue(TrailingCountProperty);
        set => SetValue(TrailingCountProperty, value);
    }

    /// <summary>Which page this item goes to.</summary>
    public ShellSection Section
    {
        get => (ShellSection)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    /// <summary>Which page the shell is on, so the item can tell whether it is the active one.</summary>
    public ShellSection ActiveSection
    {
        get => (ShellSection)GetValue(ActiveSectionProperty);
        set => SetValue(ActiveSectionProperty, value);
    }

    public ShellNavItem()
    {
        this.InitializeComponent();
        UpdateVisuals();
    }

    static void OnVisualsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is ShellNavItem navItem)
        {
            navItem.UpdateVisuals();
        }
    }

    void UpdateVisuals()
    {
        ItemGlyph.Glyph = Glyph;
        ItemLabel.Text = Label;
        ItemTrailingCount.Text = TrailingCount;
        ItemTrailingCount.Visibility = string.IsNullOrEmpty(TrailingCount)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var isActive = Section == ActiveSection;

        ActiveBar.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        ItemLabel.FontWeight = isActive
            ? Microsoft.UI.Text.FontWeights.SemiBold
            : Microsoft.UI.Text.FontWeights.Normal;

        // Dimmed rather than repainted with the secondary brush. Theme dictionary entries resolve
        // through {ThemeResource} in XAML and cannot be read out of Application.Current.Resources,
        // so fetching a brush here throws and takes the whole XAML load down with it. The tokens
        // define secondary text as primary at 55% anyway, so this lands on the same colour.
        var inactiveOpacity = 0.55;
        ItemLabel.Opacity = isActive ? 1.0 : inactiveOpacity;
        ItemGlyph.Opacity = isActive ? 1.0 : inactiveOpacity;
    }

    void ItemButton_Click(object sender, RoutedEventArgs e)
    {
        Invoked?.Invoke(this, Section);
    }
}
