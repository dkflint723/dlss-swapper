namespace DLSS_Swapper.Data;

/// <summary>
/// One vendor's worth of out of date dlls in a game.
/// </summary>
/// <remarks>
/// A game can be behind on more than one vendor at once, so these come as a list rather than a
/// single value. Cyberpunk 2077 for example can trail on both FSR and XeSS at the same time.
/// </remarks>
public class DllVendorUpdate
{
    public required DllVendor Vendor { get; init; }

    /// <summary>
    /// Short technology name shown on the badge, such as "XeSS".
    /// </summary>
    /// <remarks>
    /// The badge reads as text rather than relying on its colour. Colour alone would be unreadable
    /// to anyone with a colour vision deficiency, and red versus green is the most common one.
    /// </remarks>
    public required string Label { get; init; }

    /// <summary>Names the specific dlls of this vendor that are behind.</summary>
    public required string ToolTip { get; init; }
}
