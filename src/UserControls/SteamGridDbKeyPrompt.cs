using System;
using System.Threading.Tasks;
using DLSS_Swapper.Data.SteamGridDb;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// Makes sure there is a SteamGridDB key before a cover search runs, and offers to set one up when
/// there is not.
/// </summary>
/// <remarks>
/// Both places that search - a game's page and the library-wide scan - go through here, so the
/// instructions for getting a key are written once and read the same wherever somebody first runs
/// into needing one.
/// </remarks>
internal static class SteamGridDbKeyPrompt
{
    /// <summary>
    /// Returns true when a search can go ahead: either a key was already set, or one was just
    /// entered and checked.
    /// </summary>
    /// <remarks>
    /// Nothing at all happens for somebody who already has a key - they never see this. Saying so
    /// here rather than at each call site is what keeps the two of them identical.
    /// </remarks>
    internal static async Task<bool> EnsureKeyAsync(XamlRoot xamlRoot, string title)
    {
        if (SteamGridDbClient.HasApiKey)
        {
            return true;
        }

        var view = new CoverArtNoKeyView();

        var dialog = new EasyContentDialog(xamlRoot)
        {
            Title = title,
            Content = view,
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
        };

        var keyWasSet = false;

        // The model closes the dialog once it has a key the api accepted, rather than the dialog
        // holding its own close open while it waits on a command.
        view.ViewModel.Finished += (sender, args) =>
        {
            keyWasSet = true;
            dialog.Hide();
        };

        _ = await dialog.ShowAsync();

        return keyWasSet;
    }
}
