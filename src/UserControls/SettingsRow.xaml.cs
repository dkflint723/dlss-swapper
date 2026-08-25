using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// One setting: what it is, what it does, and the control that changes it.
/// </summary>
/// <remarks>
/// The settings page was ten repetitions of the same three elements, and the explanation was an
/// italic caption underneath the control rather than beside the name, so a setting could only be
/// understood by reading past the thing you were about to change. Written once, every setting now
/// has to have an answer to "and what does that do".
/// </remarks>
public sealed partial class SettingsRow : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty, OnVisualsChanged));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty, OnVisualsChanged));

    public static readonly DependencyProperty ControlProperty = DependencyProperty.Register(
        nameof(Control), typeof(object), typeof(SettingsRow), new PropertyMetadata(null, OnVisualsChanged));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty, OnVisualsChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>One line saying what turning this on actually does.</summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// A mark before the title, for rows that can be done something to beyond using their control —
    /// today, the libraries that can be dragged into a different order.
    /// </summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>The toggle, dropdown or button that changes it.</summary>
    public object? Control
    {
        get => GetValue(ControlProperty);
        set => SetValue(ControlProperty, value);
    }

    public SettingsRow()
    {
        this.InitializeComponent();
        UpdateVisuals();
    }

    static void OnVisualsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SettingsRow settingsRow)
        {
            settingsRow.UpdateVisuals();
        }
    }

    void UpdateVisuals()
    {
        TitleText.Text = Title;
        DescriptionText.Text = Description;

        // A setting with nothing to say about it takes no room saying it, rather than leaving a gap
        // where every other row has a sentence.
        DescriptionText.Visibility = string.IsNullOrEmpty(Description)
            ? Visibility.Collapsed
            : Visibility.Visible;

        LeadingGlyph.Glyph = Glyph;
        LeadingGlyph.Visibility = string.IsNullOrEmpty(Glyph) ? Visibility.Collapsed : Visibility.Visible;

        ControlHost.Content = Control;

        // Ties the hosted control to this row's title for a screen reader. The title is a sibling
        // TextBlock two columns away and nothing connected them, so every setting announced as its
        // control and nothing else - sixteen or more on the two most used pages all reading "Yes,
        // on, toggle switch".
        //
        // Done here rather than on each control that gets hosted: one rule, one place, and every
        // row on every page inherits it rather than each remembering.
        if (Control is UIElement hostedControl)
        {
            AutomationProperties.SetLabeledBy(hostedControl, TitleText);
        }
    }
}
