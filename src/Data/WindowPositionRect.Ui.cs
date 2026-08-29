using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace DLSS_Swapper.Data;

/// <summary>
/// A stored window position, in the Windows App SDK's own types.
/// </summary>
/// <remarks>
/// The rectangle is four numbers and a state, read out of the settings file by things that never
/// open a window, so it lives in DLSS.Swapper.Data. Turning it into a RectInt32 or reading one back
/// off an AppWindow needs the SDK, so that is here - as extensions, because a partial class cannot
/// span two assemblies.
/// </remarks>
internal static class WindowPositionRectExtensions
{

    public static RectInt32 GetRectInt32(this WindowPositionRect rect)
    {
        // LEGACY: Restore broken windows to correct positions.
        // -32000 is some magic number were windows go to die.
        // This is to help apps that are already broken to show the main window again.
        if (rect.X == -32000)
        {
            rect.X = 0;
        }

        if (rect.Y == -32000)
        {
            rect.Y = 0;
        }

        return new RectInt32(rect.X, rect.Y, rect.Width, rect.Height);
    }

    public static void UpdatePosition(this WindowPositionRect rect, PointInt32 position)
    {
        rect.X = position.X;
        rect.Y = position.Y;
    }

    public static void UpdateFromAppWindow(this WindowPositionRect rect, AppWindow appWindow)
    {
        rect.Width = appWindow.Size.Width;
        rect.Height = appWindow.Size.Height;
        rect.X = appWindow.Position.X;
        rect.Y = appWindow.Position.Y;
    }
}
