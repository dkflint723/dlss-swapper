using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.Data;

/// <summary>
/// The parts of a game that need a window in front of them.
/// </summary>
/// <remarks>
/// <para>
/// Game itself is compiled into DLSS.Swapper.Data, which has no UI - that is what lets the command
/// line reach a game and a swap without the Windows App SDK behind it. These two are the only
/// members that ever needed a window, and both are about cover art rather than about swapping.
/// </para>
/// <para>
/// Extension methods rather than another part of the class, because a partial class cannot span two
/// assemblies. They still read as game.PromptTo... at every call site, which is the point.
/// </para>
/// </remarks>
internal static class GameCoverPrompts
{

    public static async Task PromptToRemoveCustomCover(this Game game)
    {
        var dialog = new EasyContentDialog(App.CurrentApp.MainWindow.Content.XamlRoot)
        {
            Title = ResourceHelper.GetString("Game_CustomCoverRemove"),
            PrimaryButtonText = ResourceHelper.GetString("General_Remove"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = ResourceHelper.GetString("Game_AreYouSureRemoveCustomCover"),
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            game.CoverImage = null;

            if (File.Exists(game.ExpectedCustomCoverImage))
            {
                File.Delete(game.ExpectedCustomCoverImage);
            }

            if (game.GameLibrary == GameLibrary.ManuallyAdded)
            {
                await game.SaveToDatabaseAsync();
            }

            // Will load default or attempt to fetch fresh.
            await game.LoadCoverImageAsync();
        }
    }

    /// <summary>
    /// Asks for an image file and makes it this game's cover.
    /// </summary>
    /// <returns>
    /// Whether a cover was actually written. False covers both a cancelled file picker and a file
    /// that could not be read, and in either case the cover already in place is untouched.
    /// </returns>
    /// <remarks>
    /// The result exists for the cover art picker, which offers this as the way out when a game
    /// cannot be found online: it has to leave its search on screen when the file picker was
    /// cancelled, and close when it was not. Comparing CoverImage either side cannot answer that,
    /// because a game that already had a custom cover has the same path before and after.
    /// </remarks>
    public static bool PromptToBrowseCustomCover(this Game game)
    {
        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);

            var fileFilters = new List<FileSystemHelper.FileFilter>()
            {
                new FileSystemHelper.FileFilter("Image files", "*.jpg; *.jpeg; *.png; *.webp"),
            };

            var coverImageFile = FileSystemHelper.OpenFile(hWnd, fileFilters, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

            //                    ViewMode = PickerViewMode.Thumbnail,


            if (string.IsNullOrWhiteSpace(coverImageFile))
            {
                return false;
            }

            // The real answer, not an assumption that opening the picker worked. The doc above
            // already promised false for a file that could not be read; this is what makes that
            // true.
            return game.AddCustomCover(coverImageFile);
        }
        catch (Exception err)
        {
            Logger.Error(err);

            return false;
        }
    }
}
