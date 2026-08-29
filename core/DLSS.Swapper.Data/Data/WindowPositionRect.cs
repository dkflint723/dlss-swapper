using System;
using System.Text.Json.Serialization;

namespace DLSS_Swapper.Data;

public partial class WindowPositionRect
{
    [JsonPropertyName("x")]
    public int X { get; set; } = -1;

    [JsonPropertyName("y")]
    public int Y { get; set; } = -1;

    [JsonPropertyName("width")]
    public int Width { get; set; } = -1;

    [JsonPropertyName("height")]
    public int Height { get; set; } = -1;

    public WindowState State { get; set; } = WindowState.Restored;

    public WindowPositionRect()
    {

    }

    public WindowPositionRect(WindowPositionRect other)
    {
        ArgumentNullException.ThrowIfNull(other);

        X = other.X;
        Y = other.Y;
        Width = other.Width;
        Height = other.Height;
        State = other.State;

        // LEGACY: Restore broken windows to correct positions.
        // -32000 is some magic number were windows go to die.
        if (X == -32000)
        {
            X = 0;
        }

        if (Y == -32000)
        {
            Y = 0;
        }
    }

    public WindowPositionRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}
