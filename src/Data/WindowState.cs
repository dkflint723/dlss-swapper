namespace DLSS_Swapper.Data;

/// <summary>
/// How the main window was left: the part of its position that is not a number.
/// </summary>
/// <remarks>
/// <para>
/// A mirror of the Windows App SDK's OverlappedPresenterState, so that the settings file - which is
/// read with no UI in front of it - does not need the SDK to express one of its own values.
/// </para>
/// <para>
/// The numbers are not in the order anybody would guess, and they matter: the settings file stores
/// this as a number, so a mirror that renumbered them would silently reopen everybody's window in
/// the wrong state. They were read off the projection rather than assumed.
/// </para>
/// </remarks>
public enum WindowState
{
    Maximized = 0,
    Minimized = 1,
    Restored = 2,
}
