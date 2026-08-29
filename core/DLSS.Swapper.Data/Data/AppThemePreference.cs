namespace DLSS_Swapper.Data;

/// <summary>
/// Which theme the app has been asked to use.
/// </summary>
/// <remarks>
/// <para>
/// This is stored in the settings file, which is read by things that never draw anything - the
/// command line among them - so it cannot be WinUI's ElementTheme. It used to be exactly that, and
/// it was the only reason Settings needed a reference to the Windows App SDK at all.
/// </para>
/// <para>
/// The values match ElementTheme's deliberately: Default 0, Light 1, Dark 2. The settings file
/// stores the number, so anybody upgrading keeps the theme they chose, and the app converts with a
/// plain cast rather than a mapping that could be got wrong.
/// </para>
/// </remarks>
public enum AppThemePreference
{
    Default = 0,
    Light = 1,
    Dark = 2,
}
