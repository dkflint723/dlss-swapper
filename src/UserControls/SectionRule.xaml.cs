using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// A labelled rule between blocks of settings.
/// </summary>
/// <remarks>
/// The uppercasing happens here rather than in the .resw. Storing a second, shouting copy of every
/// heading would double the strings translators maintain, and casing is not a property of the words
/// -- it is how this control chooses to draw them. Done with the current culture, because which
/// letter is the capital of which is a language's business: Turkish has two i's and gets it wrong
/// under an invariant upper.
/// </remarks>
public sealed partial class SectionRule : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(SectionRule), new PropertyMetadata(string.Empty, OnLabelChanged));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public SectionRule()
    {
        this.InitializeComponent();
        UpdateVisuals();
    }

    static void OnLabelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SectionRule sectionRule)
        {
            sectionRule.UpdateVisuals();
        }
    }

    void UpdateVisuals()
    {
        LabelText.Text = Label.ToUpper(CultureInfo.CurrentCulture);
    }
}
