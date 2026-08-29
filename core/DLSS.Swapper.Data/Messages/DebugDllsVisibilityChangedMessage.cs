using CommunityToolkit.Mvvm.Messaging.Messages;

namespace DLSS_Swapper.Messages;

/// <summary>
/// Sent when the "allow debug dlls" setting changes.
/// </summary>
/// <remarks>
/// The library page holds a filtered view that is only rebuilt when you change tab, so without this
/// the toggle appeared to do nothing until you navigated away and back.
/// </remarks>
internal class DebugDllsVisibilityChangedMessage : ValueChangedMessage<bool>
{
    public DebugDllsVisibilityChangedMessage(bool allowDebugDlls) : base(allowDebugDlls)
    {
    }
}
