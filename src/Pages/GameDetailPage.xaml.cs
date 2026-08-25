using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DLSS_Swapper.Pages;

/// <summary>
/// One game, as a page.
/// </summary>
/// <remarks>
/// It was a FakeContentDialog, which exists only because a real ContentDialog cannot be open while
/// another is open on the same XamlRoot — and this surface opens six of them over itself. A page
/// has no such problem, so every one of those is now an ordinary dialog on this page's XamlRoot,
/// and the two footer ControlTemplates that used to be injected into the dialog's own template at
/// OnApplyTemplate are just a row.
///
/// Constructed fresh per game and deliberately not cached: it is about one game, and a cached
/// instance would be about whichever game was opened first.
/// </remarks>
public sealed partial class GameDetailPage : Page
{
    public const string PageTag = "GameDetail";

    public GameControlModel ViewModel { get; private set; }

    public GameDetailPage(Game game)
    {
        this.InitializeComponent();

        ViewModel = new GameControlModel(this, game);
        DataContext = ViewModel;
    }

    void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.CloseCommand.Execute(null);
        args.Handled = true;
    }
    string[] customCoverValidFileTypes = new string[]
    {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp",
            ".bmp",
    };

    DataPackageOperation coverDragDropAcceptedOperation = DataPackageOperation.None;
    string coverDragDropDragUIOverrideCaption = string.Empty;

    async void CoverButton_DragEnter(object sender, DragEventArgs e)
    {

        // This thing likes to break so I took the advice from this thread https://github.com/microsoft/microsoft-ui-xaml/issues/8108

        // Default to this.
        coverDragDropAcceptedOperation = DataPackageOperation.None;
        coverDragDropDragUIOverrideCaption = string.Empty;

        e.AcceptedOperation = coverDragDropAcceptedOperation;
        e.DragUIOverride.Caption = coverDragDropDragUIOverrideCaption;

        // This await messes things up. So what we do is also handle in CoverButton_DragOver which will have hopefully
        // mean this code is finished by then.
        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 1)
        {
            var storageFile = items[0] as StorageFile;

            if (storageFile is null)
            {
                coverDragDropAcceptedOperation = DataPackageOperation.None;
                coverDragDropDragUIOverrideCaption = ResourceHelper.GetString("GamePage_StorageFileIsNull");
            }
            else if (customCoverValidFileTypes.Contains(storageFile.FileType.ToLower()) == true)
            {
                coverDragDropAcceptedOperation = DataPackageOperation.Copy;
                coverDragDropDragUIOverrideCaption = ResourceHelper.GetString("GamePage_AddCustomCover");
            }
            else
            {
                coverDragDropAcceptedOperation = DataPackageOperation.None;
                coverDragDropDragUIOverrideCaption = ResourceHelper.GetFormattedResourceTemplate("GamePage_InvalidFileTypeTemplate", storageFile.FileType);
            }
        }
        else
        {
            coverDragDropAcceptedOperation = DataPackageOperation.None;
            coverDragDropDragUIOverrideCaption = ResourceHelper.GetString("GamePage_YouMayOnlyDragOneFileCover");
        }
    }


    void CoverButton_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = coverDragDropAcceptedOperation;
        e.DragUIOverride.Caption = coverDragDropDragUIOverrideCaption;
    }


    async void CoverButton_Drop(object sender, DragEventArgs e)
    {
        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 1)
        {
            var storageFile = items[0] as StorageFile;
            if (storageFile is null)
            {
                Logger.Error("storageFile is null");
            }
            else if (customCoverValidFileTypes.Contains(storageFile.FileType.ToLower()) == true)
            {
                using (var stream = await storageFile.OpenStreamForReadAsync())
                {
                    if (DataContext is GameControlModel gameControlModel)
                    {
                        // Off the UI thread, like every other caller. Decoding, resampling and
                        // re-encoding a dropped image runs inline otherwise, and the filter accepts
                        // any jpg, png or webp - so a photo-sized drop froze the window.
                        var game = gameControlModel.Game;

                        if (await Task.Run(() => game.AddCustomCover(stream)) == false)
                        {
                            Logger.Error($"Could not use the dropped image as a cover for {game.Title}.");
                        }
                    }
                }
            }
            else
            {
                Logger.Error($"\"{storageFile.FileType}\" is an invalid file type");
            }
        }
        else
        {
            Logger.Error("You may only drag over a single cover");
        }
    }

}

