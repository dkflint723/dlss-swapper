namespace DLSS_Swapper.Pages;

/// <summary>
/// Whether the games page shows a grid of covers or a list of rows.
/// </summary>
/// <remarks>
/// Declared with the settings rather than with the page that reads it, because the settings file is
/// loaded by things that have no pages - the command line among them - and an enum belonging to a
/// XAML page would have dragged that page's assembly along with it.
/// </remarks>
public enum GameGridViewType
{
    GridView,
    ListView,
}
