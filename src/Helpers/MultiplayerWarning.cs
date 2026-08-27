using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DLSS_Swapper.UserControls;

namespace DLSS_Swapper.Helpers;

/// <summary>
/// The one-time note that anti-cheat can object to swapped dlls, shown at the moment it matters.
/// </summary>
/// <remarks>
/// <para>
/// This used to be a modal over the loading screen: the first thing a brand new user saw was a
/// warning about an action they had not taken, before the app had shown them anything at all -
/// which trains people to dismiss dialogs unread. It now appears once, immediately before the
/// first thing that would actually write a dll into a game folder, where the same words are a
/// decision aid rather than a toll gate.
/// </para>
/// <para>
/// The covered entry points, so a future write path knows to call this too:
/// the batch runner (GameGridPageModel.RunUpdateBatchAsync), which every update route funnels
/// into - the preview sheet's confirm and the per-row action both end there - and the per-dll
/// picker (GameControlModel.ChangeRecordAsync), gated BEFORE the picker dialog opens because
/// WinUI allows one ContentDialog per root and showing this inside the picker would throw.
/// Backup and restore paths put originals back and need no gate.
/// </para>
/// </remarks>
internal static class MultiplayerWarning
{
    internal static async Task EnsureShownAsync(XamlRoot xamlRoot)
    {
        if (Settings.Instance.HasShownMultiplayerWarning)
        {
            return;
        }

        var dialog = new EasyContentDialog(xamlRoot)
        {
            Title = ResourceHelper.GetString("MainWindow_NoteForMultiplayerGames_Title"),
            CloseButtonText = ResourceHelper.GetString("General_Okay"),
            DefaultButton = ContentDialogButton.Close,
            Content = ResourceHelper.GetString("MainWindow_NoteForMultiplayerGames_Message"),
        };

        await dialog.ShowAsync();

        Settings.Instance.HasShownMultiplayerWarning = true;
    }
}
