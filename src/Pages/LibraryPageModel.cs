using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Collections;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.NVIDIA;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Extensions;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.UserControls;
using DLSS_Swapper.Versioning;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Windows.ApplicationModel.DataTransfer;

namespace DLSS_Swapper.Pages;

public partial class LibraryPageModel : ObservableObject
{
    readonly LibraryPage _libraryPage;

    /// <summary>
    /// The records shown on the library page.
    /// </summary>
    /// <remarks>
    /// A view over the master collection rather than a copy of it, so records added by an import or
    /// a manifest refresh still show up without the page being rebuilt.
    /// </remarks>
    internal AdvancedCollectionView? SelectedLibraryList { get; private set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }


    public LibraryPageModelTranslationProperties TranslationProperties { get; } = new LibraryPageModelTranslationProperties();

    public LibraryPageModel(LibraryPage libraryPage)
    {
        _libraryPage = libraryPage;

        // TODO: Change order based on prefered upscaler.
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            UpscalerTypes.Add(new UpscalerTypeItem() { AssetType = dllTypeDefinition.AssetType });
        }

        RefreshUpscalerTypes();

        if (UpscalerTypes.Count > 0)
        {
            SelectUpscalerType(UpscalerTypes[0]);
        }

        LanguageManager.Instance.OnLanguageChanged += OnLanguageChanged;

        // The list is a filtered view built when you change engine, so without this the debug dll
        // toggle appeared to do nothing until you navigated away and back.
        WeakReferenceMessenger.Default.Register<Messages.DebugDllsVisibilityChangedMessage>(this, (sender, message) =>
        {
            App.CurrentApp.RunOnUIThread(() =>
            {
                RefreshUpscalerTypes();
                SelectLibrary(SelectedAssetType);
            });
        });
    }

    /// <summary>The engines down the left of the page.</summary>
    public ObservableCollection<UpscalerTypeItem> UpscalerTypes { get; } = new ObservableCollection<UpscalerTypeItem>();

    [ObservableProperty]
    public partial GameAssetType SelectedAssetType { get; set; }

    /// <summary>
    /// Switches the page to an engine.
    /// </summary>
    /// <remarks>
    /// The selected flag lives on the items rather than being worked out from a selected index, so
    /// the row that draws the accent bar and the list that is shown come from the same answer.
    /// </remarks>
    [RelayCommand]
    void SelectUpscalerType(UpscalerTypeItem? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (var upscalerType in UpscalerTypes)
        {
            upscalerType.IsSelected = upscalerType == item;
        }

        SelectedAssetType = item.AssetType;
        SelectLibrary(item.AssetType);
    }

    /// <summary>
    /// Recounts every engine against what is currently being searched for.
    /// </summary>
    /// <remarks>
    /// Every path that recounts goes through here — construction, a language change, a refresh, and
    /// the debug-dll setting changing — so none of them can quietly revert the column to counting
    /// the whole library while a search is on.
    /// </remarks>
    void RefreshUpscalerTypes()
    {
        foreach (var upscalerType in UpscalerTypes)
        {
            upscalerType.Refresh(SearchText);
        }
    }

    /// <summary>What is typed in the page's search box.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchActiveVisibility))]
    public partial string SearchText { get; set; } = string.Empty;

    public Visibility SearchActiveVisibility => string.IsNullOrWhiteSpace(SearchText)
        ? Visibility.Collapsed
        : Visibility.Visible;

    /// <summary>Reads as "12 of 108 DLSS versions match".</summary>
    [ObservableProperty]
    public partial string SearchSummary { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        ApplySearch();
    }

    /// <summary>
    /// Narrows the list, the engine counts and the release-line headings together.
    /// </summary>
    /// <remarks>
    /// The filter is applied to the collection the groups are built FROM, not to the groups. The
    /// headings are derived from whatever list <see cref="DllVersionGroup.Build"/> is handed —
    /// which lines exist, which three stand alone, and what the rolled-up tail is called — so
    /// filtering downstream would leave "DLSS 3.6 and older" sitting over rows from another line
    /// entirely, and no amount of hiding empty groups would correct a wrong label.
    /// </remarks>
    void ApplySearch()
    {
        ApplyRecordFilter();
        RefreshUpscalerTypes();
        RebuildVersionGroups();
    }

    void ApplyRecordFilter()
    {
        if (SelectedLibraryList is null)
        {
            return;
        }

        // Assigned rather than mutated: an AdvancedCollectionView only refreshes when its Filter is
        // set, so the same predicate object put back would change nothing.
        var query = SearchText;
        var allowDebugDlls = Settings.Instance.AllowDebugDlls;

        SelectedLibraryList.Filter = x => x is DLLRecord record && DllSearch.Passes(record, query, allowDebugDlls);
    }

    /// <summary>Puts the whole engine back, and says so in words rather than with a glyph.</summary>
    [RelayCommand]
    void ClearSearch()
    {
        SearchText = string.Empty;
    }

    void OnLanguageChanged()
    {
        RefreshUpscalerTypes();
    }

    [RelayCommand]
    async Task RefreshAsync()
    {
        IsRefreshing = true;

        var didUpdate = await DLLManager.Instance.UpdateManifestAsync();

        if (didUpdate)
        {
            // Reload selected library. The counts down the left move with it, since a refreshed
            // manifest is exactly when the number of known versions changes.
            RefreshUpscalerTypes();
            SelectLibrary(SelectedAssetType);
        }
        else
        {
            var errorDialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Error"),
                CloseButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Close,
                Content = ResourceHelper.GetString("LibraryPage_UnableToUpdateDllRecord"),
            };
            await errorDialog.ShowAsync();
        }

        IsRefreshing = false;
    }

    [RelayCommand]
    async Task ExportAllAsync()
    {
        // Check that there are records to export first.
        var allDllRecords = new List<DLLRecord>();
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            var records = DLLManager.Instance.GetRecords(dllTypeDefinition.AssetType);
            if (records is null)
            {
                continue;
            }

            allDllRecords.AddRange(records.Where(x => x.LocalRecord?.IsDownloaded == true));
        }

        if (allDllRecords.Count == 0)
        {
            var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                CloseButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Close,
                Title = ResourceHelper.GetString("General_Error"),
                Content = ResourceHelper.GetString("LibraryPage_NoDllsToExport"),
            };
            await dialog.ShowAsync();
            return;
        }



        var filesProgressBar = new ProgressBar()
        {
            IsIndeterminate = true
        };
        var progressTextBlock = new TextBlock()
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        progressTextBlock.Inlines.Add(new Run() { Text = ResourceHelper.GetString("LibraryPage_ExportedDLLs"), FontWeight = FontWeights.Bold });
        var progressRun = new Run() { Text = "0" };
        progressTextBlock.Inlines.Add(progressRun);
        var progressStackPanel = new StackPanel()
        {
            Spacing = 16,
            Orientation = Orientation.Vertical,
            Children =
            {
                filesProgressBar,
                progressTextBlock,
            }
        };

        var str = ResourceHelper.GetString("LibraryPage_Exporting");
        var exportingDialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("LibraryPage_Exporting"),
            Content = progressStackPanel,
        };

        var tempExportPath = Path.Combine(Storage.GetTemp(), "export");
        var finalExportZip = string.Empty;
        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);

            var fileFilters = new List<FileSystemHelper.FileFilter>()
            {
                new FileSystemHelper.FileFilter("Zip files", "*.zip"),
            };

            finalExportZip = FileSystemHelper.SaveFile(hWnd, fileFilters, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "dlss_swapper_export.zip", defaultExtension: "zip");

            // User cancelled.
            if (string.IsNullOrWhiteSpace(finalExportZip))
            {
                return;
            }

            Storage.CreateDirectoryIfNotExists(tempExportPath);

            _ = exportingDialog.ShowAsync();

            // Give UI time to update and show export loading wheel.
            await Task.Delay(50);

            var toExport = new List<(string SourceFileName, string EntryName)>();

            foreach (var dllRecord in allDllRecords)
            {
                if (dllRecord.LocalRecord is null || dllRecord.LocalRecord.IsDownloaded == false)
                {
                    continue;
                }

                var expectedPathDirectory = Path.GetDirectoryName(dllRecord.LocalRecord.ExpectedPath);
                if (string.IsNullOrWhiteSpace(expectedPathDirectory))
                {
                    continue;
                }

                // TODO: When fixing imported system, make sure to update this to use full path
                var internalZipDir = DLLManager.Instance.GetAssetTypeName(dllRecord.AssetType);
                if (dllRecord.LocalRecord.IsImported == true)
                {
                    internalZipDir = Path.Combine("Imported", internalZipDir);
                }
                var directoryInfo = new DirectoryInfo(expectedPathDirectory);

                internalZipDir = Path.Combine(internalZipDir, directoryInfo.Name);

                toExport.Add((dllRecord.LocalRecord.ExpectedPath, Path.Combine(internalZipDir, Path.GetFileName(dllRecord.LocalRecord.ExpectedPath))));
            }


            Exception? exportError = null;

            if (toExport.Count == 0)
            {
                exportingDialog.Hide();

                var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetString("LibraryPage_NoDLLsForExport_Message"),
                };
                await dialog.ShowAsync();
            }
            else
            {
                filesProgressBar.IsIndeterminate = false;
                filesProgressBar.Value = 0;
                filesProgressBar.Maximum = toExport.Count;

                var progress = new Progress<int>();
                progress.ProgressChanged += (s, i) =>
                {
                    filesProgressBar.Value = i;
                    progressRun.Text = i.ToString(CultureInfo.CurrentCulture);
                };

                await Task.Run(() =>
                {
                    exportError = ExportDllWorker(finalExportZip, toExport, progress);
                });

                exportingDialog.Hide();

                if (exportError is null)
                {
                    // With a way to the file it just made. "Exported 12 DLLs. [Okay]" left the one
                    // thing the user wanted - the zip - to be hunted down by hand.
                    var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
                    {
                        PrimaryButtonText = ResourceHelper.GetString("LibraryPage_ShowInFolder"),
                        CloseButtonText = ResourceHelper.GetString("General_Okay"),
                        DefaultButton = ContentDialogButton.Primary,
                        Title = ResourceHelper.GetString("General_Success"),
                        Content = ResourceHelper.GetFormattedResourceTemplate("LibraryPage_ExportedDLLsCount_Message", toExport.Count),
                    };

                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        FileSystemHelper.OpenFolderInExplorerSelectFile(finalExportZip);
                    }
                }
                else
                {
                    throw new Exception("Worker thread failed to export.", exportError);
                }
            }
        }
        catch (Exception err)
        {
            // If we failed to export lets delete teh temp zip file that was create.
            if (string.IsNullOrEmpty(finalExportZip) == false && File.Exists(finalExportZip))
            {
                try
                {
                    if (File.Exists(finalExportZip))
                    {
                        File.Delete(finalExportZip);
                    }
                }
                catch (Exception err2)
                {
                    Logger.Error(err2);
                }
            }

            exportingDialog.Hide();

            Logger.Error(err);

            // If the fullExpectedPath does not exist, or there was an error writing it.
            var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Error"),
                CloseButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Close,
                Content = ResourceHelper.GetString("LibraryPage_CouldntExportDll"),
            };
            await dialog.ShowAsync();
        }
        finally
        {
            // Clean up temp export path.
            try
            {
                if (Directory.Exists(tempExportPath))
                {
                    Directory.Delete(tempExportPath, true);
                }
            }
            catch (Exception err)
            {
                Logger.Error(err);
            }
        }
    }

    Exception? ExportDllWorker(string zipPath, List<(string SourceFileName, string EntryName)> filesToAdd, IProgress<int>? progress)
    {
        try
        {
            using (var fileStream = File.Create(zipPath))
            {
                using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    var exported = 0;
                    foreach (var fileToAdd in filesToAdd)
                    {
                        zipArchive.CreateEntryFromFile(fileToAdd.SourceFileName, fileToAdd.EntryName);
                        ++exported;

                        progress?.Report(exported);
                    }
                }
            }

            return null;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            return err;
        }
    }


    [RelayCommand]
    async Task ImportAsync()
    {
        if (DLLManager.Instance.ImportedManifest is null)
        {
            var couldNotImportDialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("LibraryPage_CouldNotLoadImportedDlls"),
                DefaultButton = ContentDialogButton.Close,
                Content = new ImportSystemDisabledView(),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
            };
            await couldNotImportDialog.ShowAsync();
            return;
        }

        if (Settings.Instance.HasShownWarning == false)
        {
            var warningDialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Warning"),
                CloseButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Close,
                Content = ResourceHelper.GetString("LibraryPage_MaliciousDllsInfo"),
            };
            await warningDialog.ShowAsync();

            Settings.Instance.HasShownWarning = true;
        }


        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);

        var fileFilters = new List<FileSystemHelper.FileFilter>()
        {
            new FileSystemHelper.FileFilter("Supported file types", "*.dll; *.zip"),
            new FileSystemHelper.FileFilter("DLL files", "*.dll"),
            new FileSystemHelper.FileFilter("ZIP files", "*.zip")
        };

        var openFileList = FileSystemHelper.OpenMultipleFiles(hWnd, fileFilters, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        // User cancelled.
        if (openFileList.Count == 0)
        {
            return;
        }

        var filesProgressBar = new ProgressBar()
        {
            IsIndeterminate = true
        };
        var dllInZipProgressBar = new ProgressBar()
        {
            IsIndeterminate = true
        };
        var progressTextBlock = new TextBlock()
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        progressTextBlock.Inlines.Add(new Run() { Text = ResourceHelper.GetString("LibraryPage_ProcessedDlls"), FontWeight = FontWeights.Bold });
        var progressRun = new Run() { Text = "0" };
        progressTextBlock.Inlines.Add(progressRun);
        var progressStackPanel = new StackPanel()
        {
            Spacing = 16,
            Orientation = Orientation.Vertical,
            Children =
            {
                filesProgressBar,
                dllInZipProgressBar,
                progressTextBlock,
            }
        };

        var loadingDialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("LibraryPage_Importing"),
            // I would like this to be a progress ring but for some reason the ring will not show.
            Content = progressStackPanel,
        };
        _ = loadingDialog.ShowAsync();

        var taskCompletionSource = new TaskCompletionSource<List<DLLImportResult>>();

        bool HandleLocalDLLRecordZip(string importedPath, DLLRecord dllRecord, List<DLLImportResult> importResults)
        {
            if (dllRecord.LocalRecord is not null)
            {
                if (File.Exists(dllRecord.LocalRecord.ExpectedPath))
                {
                    App.CurrentApp.RunOnUIThread(() =>
                    {
                        var localRecord = dllRecord.LocalRecord;
                        localRecord.IsDownloaded = true;
                        dllRecord.LocalRecord = null;
                        dllRecord.LocalRecord = localRecord;
                    });

                    importResults.Add(DLLImportResult.FromSucces(dllRecord.LocalRecord.ExpectedPath, ResourceHelper.GetString("LibraryPage_AlreadyDownloaded"), true));
                    return true;
                }

                try
                {
                    using (var fileStream = File.OpenRead(importedPath))
                    {
                        using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read, true))
                        {
                            DLLManager.HandleExtractFromZip(zipArchive, dllRecord);
                        }
                    }
                }
                catch (Exception err)
                {
                    Logger.Error(err);
                    importResults.Add(DLLImportResult.FromFail(importedPath, "Failed to extract DLL from zip."));
                    return false;
                }

                App.CurrentApp.RunOnUIThread(() =>
                {
                    var localRecord = dllRecord.LocalRecord;
                    localRecord.IsDownloaded = true;
                    dllRecord.LocalRecord = null;
                    dllRecord.LocalRecord = localRecord;
                });
                importResults.Add(DLLImportResult.FromSucces(importedPath, ResourceHelper.GetString("LibraryPage_ImportedAsExistingRecord"), true));
                return true;
            }
            else
            {
                // This should never happen.
                Logger.Error("dllRecord.LocalRecord is null");
                Debugger.Break();
                importResults.Add(DLLImportResult.FromFail(importedPath, "dllRecord.LocalRecord is null"));
                return false;
            }
        }

        if (openFileList.Count == 1)
        {
            filesProgressBar.Visibility = Visibility.Collapsed;
        }
        else
        {
            filesProgressBar.IsIndeterminate = false;
        }

        filesProgressBar.Value = 0;
        filesProgressBar.Maximum = openFileList.Count;

        var selectedFilesProcessed = 0;
        var totalDllsProcessed = 0;

        ThreadPool.QueueUserWorkItem((stateInfo) =>
        {
            var importResults = new List<DLLImportResult>();

            // Used only if we import a zip
            var tempExtractPath = Path.Combine(Storage.GetTemp(), "import", Guid.NewGuid().ToString("D"));
            Storage.CreateDirectoryIfNotExists(tempExtractPath);


            foreach (var importFile in openFileList)
            {
                // Snapshotted before the lambda, not read inside it. These updates are queued to
                // the UI thread from a thread pool item that keeps counting, so a lambda reading the
                // counter directly reported whatever it had reached by the time the queue got round
                // to it - the bar and the "processed" number could describe different files.
                ++selectedFilesProcessed;
                var filesDone = selectedFilesProcessed;
                App.CurrentApp.RunOnUIThread(() =>
                {
                    filesProgressBar.Value = filesDone;
                });

                if (importFile is null || File.Exists(importFile) == false)
                {
                    importResults.Add(DLLImportResult.FromFail(importFile ?? string.Empty, ResourceHelper.GetString("LibraryPage_FileNotFound")));
                    continue;
                }

                try
                {
                    if (importFile.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // If we are importing a zip, first check if its hash is one
                        // that we expect.Then we can just bypass everything.
                        var newZipHash = string.Empty;
                        using (var fileStream = File.OpenRead(importFile))
                        {
                            newZipHash = fileStream.GetMD5Hash();
                        }

                        if (string.IsNullOrWhiteSpace(newZipHash) == false)
                        {
                            // Match the zip against every known dll type instead of repeating the
                            // same lookup once per type.
                            var handledKnownZip = false;
                            foreach (var dllTypeDefinition in DllTypes.All)
                            {
                                var zipRecord = DLLManager.Instance.GetRecords(dllTypeDefinition.AssetType)?
                                    .FirstOrDefault(x => string.Equals(x.ZipMD5Hash, newZipHash, StringComparison.InvariantCultureIgnoreCase));

                                if (zipRecord is null)
                                {
                                    continue;
                                }

                                if (HandleLocalDLLRecordZip(importFile, zipRecord, importResults))
                                {
                                    ++totalDllsProcessed;
                                    var knownZipDllsDone = totalDllsProcessed;
                                    App.CurrentApp.RunOnUIThread(() =>
                                    {
                                        progressRun.Text = knownZipDllsDone.ToString(CultureInfo.CurrentCulture);
                                    });
                                    handledKnownZip = true;
                                    break;
                                }
                            }

                            // Each of the nine blocks this replaced ended in a continue that skipped
                            // to the next import file, not to the next dll type.
                            if (handledKnownZip)
                            {
                                continue;
                            }
                        }


                        // Now that we know the zip itself is not a known zip we will extract each DLL and import them.
                        using (var archive = ZipFile.OpenRead(importFile))
                        {
                            var zippedDlls = archive.Entries.Where(x => x.Name.EndsWith(".dll")).ToArray();
                            if (zippedDlls.Length == 0)
                            {
                                throw new Exception(ResourceHelper.GetString("LibraryPage_ZipDidNotContainAnyDlls"));
                            }

                            var dllsInZip = zippedDlls.Length;
                            var processedDllsInZip = 0;

                            App.CurrentApp.RunOnUIThread(() =>
                            {
                                dllInZipProgressBar.IsIndeterminate = false;

                                // Zero, said as zero. This is the reset before the zip's own loop
                                // starts, and reading the counter here meant it could arrive after
                                // the loop had moved on and set the bar backwards.
                                dllInZipProgressBar.Value = 0;
                                dllInZipProgressBar.Maximum = dllsInZip;
                            });

                            foreach (var zippedDll in zippedDlls)
                            {
                                // The name inside the zip is the archive author's to choose, and it
                                // is not always a bare file name: .NET keeps the separators when an
                                // entry claims to have been made on Unix, so "..\..\evil.dll"
                                // arrives intact, passes the .dll filter above, and Path.Combine
                                // resolves it outside the folder it was meant to go in. Reproduced,
                                // not guessed - see ZipEntryPath, which is where the rule and its
                                // tests live.
                                var entryFolder = Path.Combine(tempExtractPath, Guid.NewGuid().ToString("D"));

                                if (ZipEntryPath.TryResolve(entryFolder, zippedDll.Name, out var tempFile) == false)
                                {
                                    Logger.Error($"Refusing to extract '{zippedDll.FullName}' from {importFile}, it does not stay inside the import folder.");
                                    importResults.Add(DLLImportResult.FromFail(importFile, ResourceHelper.GetString("LibraryPage_ZipEntryEscapedTheImportFolder")));
                                    continue;
                                }

                                Storage.CreateDirectoryForFileIfNotExists(tempFile);

                                zippedDll.ExtractToFile(tempFile, true);

                                ++processedDllsInZip;
                                ++totalDllsProcessed;
                                var zipDllsDone = processedDllsInZip;
                                var allDllsDone = totalDllsProcessed;
                                App.CurrentApp.RunOnUIThread(() =>
                                {
                                    dllInZipProgressBar.Value = zipDllsDone;
                                    progressRun.Text = allDllsDone.ToString(CultureInfo.CurrentCulture);
                                });


                                try
                                {
                                    // In future when DLLs will have multiple per bundle we will have to extract them all and pass them as a list.
                                    importResults.Add(DLLManager.Instance.ImportDll(tempFile, zippedDll.FullName));
                                }
                                catch (Exception err)
                                {
                                    Logger.Error(err);
                                    importResults.Add(DLLImportResult.FromFail(zippedDll.FullName, err.Message));
                                }

                                // Clean up temp file.
                                File.Delete(tempFile);
                            }
                        }
                    }
                    else if (importFile.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
                    {
                        try
                        {
                            importResults.Add(DLLManager.Instance.ImportDll(importFile));
                        }
                        catch (Exception err)
                        {
                            Logger.Error(err);
                            importResults.Add(DLLImportResult.FromFail(importFile, err.Message));
                        }

                        ++totalDllsProcessed;
                        var looseDllsDone = totalDllsProcessed;
                        App.CurrentApp.RunOnUIThread(() =>
                        {
                            progressRun.Text = looseDllsDone.ToString(CultureInfo.CurrentCulture);
                        });
                    }
                }
                catch (Exception err)
                {
                    Logger.Error(err);
                    importResults.Add(DLLImportResult.FromFail(importFile, err.Message));
                }
            }

            // Clean up tempExtractPath if it exists
            if (Directory.Exists(tempExtractPath))
            {
                try
                {
                    Directory.Delete(tempExtractPath, true);
                }
                catch (Exception err2)
                {
                    Logger.Error(err2);
                }
            }

            taskCompletionSource.SetResult(importResults);
        });

        var importResults = await taskCompletionSource.Task;

        if (importResults.Any(x => x.Success == true))
        {
            await DLLManager.Instance.SaveImportedManifestJsonAsync();

            // An imported dll can be newer than anything a game has, so which games are behind has
            // to be worked out again. Nothing else does it: ImportDll puts the record straight into
            // the list the upscalers page shows, while the games page only recounts when
            // GamesChanged fires, and importing never raised it.
            GameManager.Instance.RefreshUpdateAvailable();
        }

        loadingDialog.Hide();

        var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            CloseButtonText = ResourceHelper.GetString("General_Okay"),
            DefaultButton = ContentDialogButton.Close,
            Title = ResourceHelper.GetString("LibraryPage_Finished"),
            Content = new ImportDLLSummaryControl(importResults),
        };
        await dialog.ShowAsync();
    }

    [RelayCommand]
    async Task ImportFromNVIDIADriverAsync()
    {
        var loadingProgressRing = new ProgressRing()
        {
            IsIndeterminate = true
        };
        var loadingDialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("LibraryPage_ImportFromNVIDIADriver"),
            Content = loadingProgressRing,
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        loadingDialog.CloseButtonClick += (ContentDialog sender, ContentDialogButtonClickEventArgs args) => {
            cancellationTokenSource.Cancel();
        };

        _ = loadingDialog.ShowAsync();

        var models = new List<NGXModel>();
        await Task.Run(() =>
        {
            models.AddRange(NVAPIHelper.Instance.GetNGXModels());
        });

        if (cancellationTokenSource.IsCancellationRequested)
        {

            loadingDialog.Hide();
            return;
        }

        if (models.Count == 0)
        {

            loadingDialog.Hide();
            var errorDialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Error"),
                Content = ResourceHelper.GetString("LibraryPage_CouldNotImportFromDriver"),
                CloseButtonText = ResourceHelper.GetString("General_Close"),
            };
            await errorDialog.ShowAsync();
            return;
        }
                
        var ngxModelImporter = new NGXModelImporter(models);

        await Task.Run(() =>
        {
            foreach (var modelRow in ngxModelImporter.ViewModel.Models)
            {
                var versionNumber = modelRow.NGXModel.Version.GetVersionNumber();

                var existingRecordsToTest = new List<DLLRecord>();

                if (modelRow.NGXModel.GameAssetType == GameAssetType.DLSS)
                {
                    var existingDLLRecords = DLLManager.Instance.DLSSRecords.Where(x => x.VersionNumber == versionNumber && x.LocalRecord is not null && x.LocalRecord.IsDownloaded);
                    existingRecordsToTest.AddRange(existingDLLRecords);
                }
                else if (modelRow.NGXModel.GameAssetType == GameAssetType.DLSS_D)
                {
                    var existingDLLRecords = DLLManager.Instance.DLSSDRecords.Where(x => x.VersionNumber == versionNumber && x.LocalRecord is not null && x.LocalRecord.IsDownloaded);
                    existingRecordsToTest.AddRange(existingDLLRecords);
                }
                else if (modelRow.NGXModel.GameAssetType == GameAssetType.DLSS_G)
                {
                    var existingDLLRecords = DLLManager.Instance.DLSSGRecords.Where(x => x.VersionNumber == versionNumber && x.LocalRecord is not null && x.LocalRecord.IsDownloaded);
                    existingRecordsToTest.AddRange(existingDLLRecords);
                }

                foreach (var existingRecordToTest in existingRecordsToTest)
                {
                    try
                    {
                        using (var fileStream = File.OpenRead(modelRow.NGXModel.FilePath))
                        {
                            var md5Hash = fileStream.GetMD5Hash();
                            if (string.Equals(md5Hash, existingRecordToTest.MD5Hash))
                            {
                                modelRow.IsEnabled = false;
                                modelRow.StatusMessage = ResourceHelper.GetString("LibraryPage_AlreadyDownloaded");
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                }
            }
        });

        loadingDialog.Hide();

        var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("LibraryPage_ImportFromNVIDIADriver"),
            DefaultButton = ContentDialogButton.Primary,
            Content = ngxModelImporter,
            PrimaryButtonText = ResourceHelper.GetString("General_Import"),
            CloseButtonText = ResourceHelper.GetString("General_Close"),
        };
        dialog.Resources["ContentDialogMinWidth"] = 700;
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var modelsToImport = new List<NGXModel>();
            foreach (var modelRow in ngxModelImporter.ViewModel.Models)
            {
                if (modelRow.IsChecked == true)
                {
                    modelsToImport.Add(modelRow.NGXModel);
                }
            }

            if (modelsToImport.Count == 0)
            {
                return;
            }


            var filesProgressBar = new ProgressBar()
            {
                IsIndeterminate = true
            };
            var progressTextBlock = new TextBlock()
            {
                Text = string.Empty,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            progressTextBlock.Inlines.Add(new Run() { Text = ResourceHelper.GetString("LibraryPage_ImportedDLLs"), FontWeight = FontWeights.Bold });
            var progressRun = new Run() { Text = "0" };
            progressTextBlock.Inlines.Add(progressRun);
            var progressStackPanel = new StackPanel()
            {
                Spacing = 16,
                Orientation = Orientation.Vertical,
                Children =
                {
                    filesProgressBar,
                    progressTextBlock,
                }
            };


            var importingDialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("LibraryPage_Importing"),
                Content = progressStackPanel,
            };

            filesProgressBar.IsIndeterminate = false;
            filesProgressBar.Value = 0;
            filesProgressBar.Maximum = modelsToImport.Count;

            _ = importingDialog.ShowAsync();

            var successCount = 0;
            var failedCount = 0;

            var failures = new List<string>();

            await Task.Run(() => {
                for (var i = 0; i < modelsToImport.Count; ++i)
                {
                    try
                    {
                        var didImport = DLLManager.Instance.ImportDll(modelsToImport[i].FilePath, overrideFileName: DLLManager.DllNameForGameAssetType(modelsToImport[i].GameAssetType));
                        if (didImport.Success)
                        {
                            ++successCount;
                        }
                        else
                        {
                            ++failedCount;
                            failures.Add(didImport.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        ++failedCount;
                        Logger.Error(ex, "Error importing NGX model.");
                    }
                    finally
                    {
                        // Snapshotted, and one past the index: the loop variable is shared across
                        // iterations, so the enqueued lambda could read a later value - and item
                        // one of N used to finish with the counter still reading zero.
                        var done = i + 1;
                        App.CurrentApp.RunOnUIThread(() =>
                        {
                            filesProgressBar.Value = done;
                            progressRun.Text = done.ToString(CultureInfo.CurrentCulture);
                        });
                    }
                }
            });

            await DLLManager.Instance.SaveImportedManifestJsonAsync();

            // As in ImportAsync: the games page has to be told the newly imported versions exist.
            GameManager.Instance.RefreshUpdateAvailable();

            importingDialog.Hide();

            // The counts, and when something failed, what failed - the loop had each failure's
            // own message and used to throw it away in favour of a bare number.
            var completeSummary = $"{ResourceHelper.GetString("General_Success")}: {successCount}\n{ResourceHelper.GetString("General_Failed")}: {failedCount}";
            if (failures.Count > 0)
            {
                completeSummary += "\n\n" + string.Join("\n", failures);
            }

            var completeDialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("LibraryPage_ImportFromNVIDIADriver"),
                DefaultButton = ContentDialogButton.Close,
                Content = completeSummary,
                CloseButtonText = ResourceHelper.GetString("General_Close"),
            };
            await completeDialog.ShowAsync();

        }
    }


    [GeneratedRegex(@"^d6e9b45e-d4f6-4a84-a460-bf61decae3e8\/(?<asset_type>dlss|dlssg|dlssd)\/versions\/(?<version_packed>\d*)\/files\/160_E658700\.bin$", RegexOptions.IgnoreCase)]
    private static partial Regex IsNGXModelWeCanUse();

    [RelayCommand]
    async Task ImportFromNVIDIAServerAsync()
    {
        var loadingProgressRing = new ProgressRing()
        {
            IsIndeterminate = true
        };
        var loadingDialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("LibraryPage_FetchingFileList"),
            Content = loadingProgressRing,
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        loadingDialog.CloseButtonClick += (ContentDialog sender, ContentDialogButtonClickEventArgs args) => {
            cancellationTokenSource.Cancel();
        };

        _ = loadingDialog.ShowAsync();

        var ngxOtaUrl = "https://ngx.download.nvidia.com";
        var xmlDownloader = new FileDownloader(ngxOtaUrl);

        var availableModels = new List<NGXModel>();

        using (var memoryStream = new MemoryStream())
        {
            try
            {
                var didDownload = await xmlDownloader.DownloadFileToStreamAsync(memoryStream, cancellationTokenSource.Token);
                if (didDownload == false)
                {
                    throw new Exception("Could not download xml stream.");
                }

                memoryStream.Position = 0;

                var serializer = new XmlSerializer(typeof(ListBucketResult));
                var listBucketResult = serializer.Deserialize(memoryStream) as ListBucketResult;

                if (listBucketResult is null)
                {
                    throw new Exception("ListBucketResult was null.");
                }


                foreach (var content in listBucketResult.Contents)
                {
                    if (content is null || content.Size == 0)
                    {
                        continue;
                    }

                    // We only give the option of 160_E658700.bin. Other files do exist.
                    // 160 is from NV_GPU_ARCHITECTURE_ID of Turing GPUs. But it appears everyone has this for DLSS files.
                    // As for what E658700, no idea.
                    // https://github.com/SimonMacer/AnWave/issues/52#issuecomment-3025720063
                    // https://docs.nvidia.com/nvapi/group__gpu.html
                    if (content.Key.EndsWith("files/160_E658700.bin") == false)
                    {
                        continue;
                    }

                    var match = IsNGXModelWeCanUse().Match(content.Key);
                    if (match.Success == false)
                    {
                        continue;
                    }

                    GameAssetType? gameAssetType = match.Groups["asset_type"].Value switch
                    {
                        "dlss" => GameAssetType.DLSS,
                        "dlssd" => GameAssetType.DLSS_D,
                        "dlssg" => GameAssetType.DLSS_G,
                        _ => null,
                    };


                    if (gameAssetType == null)
                    {
                        continue;
                    }

                    if (Int32.TryParse(match.Groups["version_packed"].ValueSpan, out var versionInt) == false)
                    {
                        Logger.Error($"Could not convert {match.Groups["version_packed"].Value} to a version number.");
                        continue;
                    }

                    var major = (versionInt >> 16) & 0xFFFF;
                    var minor = (versionInt >> 8) & 0xFF;
                    var build = versionInt & 0xFF;
                    var version = new Version(major, minor, build, 0);

                    availableModels.Add(new NGXModel($"{ngxOtaUrl}/{content.Key}", version, gameAssetType.Value, (long)content.Size, content.ETag));
                }

            }
            catch (TaskCanceledException) when (cancellationTokenSource.IsCancellationRequested)
            {
                // NOOP: User cancelled
                return;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);

                loadingDialog.Hide();

                var errorDialog = new EasyContentDialog(_libraryPage.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    Content = ResourceHelper.GetString("LibraryPage_Error_NVIDIA_Importing"),
                    CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                };
                await errorDialog.ShowAsync();
                return;
            }
        }


        if (availableModels.Count == 0)
        {
            loadingDialog.Hide();
            var errorDialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Error"),
                Content = ResourceHelper.GetString("LibraryPage_Error_NVIDIA_Downloading"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            };
            await errorDialog.ShowAsync();
            return;
        }

        var ngxModelImporter = new NGXModelImporter(availableModels);

        foreach (var modelRow in ngxModelImporter.ViewModel.Models)
        {
            var versionNumber = modelRow.NGXModel.Version.GetVersionNumber();

            var existingRecordsToTest = new List<DLLRecord>();

            if (modelRow.NGXModel.GameAssetType == GameAssetType.DLSS)
            {
                var existingDLLRecords = DLLManager.Instance.DLSSRecords.Where(x => x.VersionNumber == versionNumber && x.LocalRecord is not null && x.LocalRecord.IsDownloaded);
                existingRecordsToTest.AddRange(existingDLLRecords);
            }
            else if (modelRow.NGXModel.GameAssetType == GameAssetType.DLSS_D)
            {
                var existingDLLRecords = DLLManager.Instance.DLSSDRecords.Where(x => x.VersionNumber == versionNumber && x.LocalRecord is not null && x.LocalRecord.IsDownloaded);
                existingRecordsToTest.AddRange(existingDLLRecords);
            }
            else if (modelRow.NGXModel.GameAssetType == GameAssetType.DLSS_G)
            {
                var existingDLLRecords = DLLManager.Instance.DLSSGRecords.Where(x => x.VersionNumber == versionNumber && x.LocalRecord is not null && x.LocalRecord.IsDownloaded);
                existingRecordsToTest.AddRange(existingDLLRecords);
            }

            foreach (var existingRecordToTest in existingRecordsToTest)
            {
                try
                {
                    if (File.Exists(existingRecordToTest?.LocalRecord?.ExpectedPath))
                    {
                        using (var fileStream = File.OpenRead(existingRecordToTest.LocalRecord.ExpectedPath))
                        {
                            var isValid = NVAPIHelper.Instance.ValidateNVIDIAOtaHash(fileStream, modelRow.NGXModel.ETag);
                            if (isValid)
                            {
                                modelRow.IsEnabled = false;
                                modelRow.StatusMessage = ResourceHelper.GetString("LibraryPage_AlreadyDownloaded");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }

        loadingDialog.Hide();

        var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("LibraryPage_DownloadFromNVIDIA"),
            DefaultButton = ContentDialogButton.Primary,
            Content = ngxModelImporter,
            PrimaryButtonText = ResourceHelper.GetString("General_Download"),
            CloseButtonText = ResourceHelper.GetString("General_Close"),
        };
        dialog.Resources["ContentDialogMinWidth"] = 700;
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var modelsToDownload = ngxModelImporter.ViewModel.Models.Where(x => x.IsChecked).ToList();
        if (modelsToDownload.Count == 0)
        {
            return;
        }


        var totalFilesProgressBar = new ProgressBar()
        {
            IsIndeterminate = false,
            Value = 0,
            Maximum = modelsToDownload.Count,
        };
        var totalFilesTextBlock = new TextBlock()
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        totalFilesTextBlock.Inlines.Add(new Run() { Text = ResourceHelper.GetString("LibraryPage_DownloadedCount"), FontWeight = FontWeights.Bold });
        var totalFilesProgressRun = new Run() { Text = "0" };
        totalFilesTextBlock.Inlines.Add(totalFilesProgressRun);


        var currentFileProgressBar = new ProgressBar()
        {
            IsIndeterminate = false,
            Value = 0,
            Maximum = 1,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var currentFileTextBlock = new TextBlock()
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        currentFileTextBlock.Inlines.Add(new Run() { Text = ResourceHelper.GetString("LibraryPage_ProgressPercent"), FontWeight = FontWeights.Bold });
        var currentFileProgressRun = new Run() { Text = "0" };
        currentFileTextBlock.Inlines.Add(currentFileProgressRun);

        var progressStackPanel = new StackPanel()
        {
            Spacing = 16,
            Orientation = Orientation.Vertical,
            Children =
            {
                totalFilesProgressBar,
                totalFilesTextBlock,
                currentFileProgressBar,
                currentFileTextBlock,
            }
        };

        var downloadingDialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("General_Downloading"),
            Content = progressStackPanel,
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
        };

        downloadingDialog.CloseButtonClick += (ContentDialog sender, ContentDialogButtonClickEventArgs args) => {
            cancellationTokenSource.Cancel();
        };

        _ = downloadingDialog.ShowAsync();

        var successCount = 0;
        var failCount = 0;

        await Task.Run(async () => {
            for (var i = 0; i < modelsToDownload.Count; ++i)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                App.CurrentApp.RunOnUIThread(() =>
                {
                    currentFileProgressBar.Value = 0;
                });

                try
                {
                    var tempFileName = $"{Guid.NewGuid().ToString("D")}.tmp";
                    var tempFilePath = Path.Combine(Storage.GetTemp(), tempFileName);

                    var didDownload = false;
                    using (var fileStream = File.Create(tempFilePath))
                    {
                        var fileDownloader = new FileDownloader(modelsToDownload[i].NGXModel.FilePath);
                        didDownload = await fileDownloader.DownloadFileToStreamAsync(fileStream, cancellationTokenSource.Token, progressCallback: (DownloadedBytes, TotalBytesToDownload, Percent) =>
                        {
                            App.CurrentApp.RunOnUIThread(() =>
                            {
                                var smallPercent = Percent / 100.0;
                                currentFileProgressBar.Value = smallPercent;
                                currentFileProgressRun.Text = smallPercent.ToString("P", CultureInfo.CurrentCulture);
                            });
                        });
                    }

                    if (didDownload)
                    {
                        var didImport = DLLManager.Instance.ImportDll(tempFilePath, overrideFileName: DLLManager.DllNameForGameAssetType(modelsToDownload[i].NGXModel.GameAssetType));
                        if (didImport.Success)
                        {
                            ++successCount;
                        }
                        else
                        {
                            ++failCount;
                        }
                    }
                    else
                    {
                        ++failCount;
                    }
                }
                catch (TaskCanceledException) when (cancellationTokenSource.IsCancellationRequested)
                {
                    // NOOP
                }
                catch (Exception ex)
                {
                    ++failCount;
                    Logger.Error(ex, "Error downloading or importing NGX model.");
                }
                finally
                {
                    App.CurrentApp.RunOnUIThread(() =>
                    {
                        totalFilesProgressBar.Value += 1;
                        totalFilesProgressRun.Text = totalFilesProgressBar.Value.ToString(CultureInfo.CurrentCulture);
                    });
                }
            }
        });


        if (cancellationTokenSource.IsCancellationRequested == false)
        {
            await DLLManager.Instance.SaveImportedManifestJsonAsync();

            // As in ImportAsync: the games page has to be told the newly imported versions exist.
            GameManager.Instance.RefreshUpdateAvailable();

            downloadingDialog.Hide();

            var completeDialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("LibraryPage_DownloadFromNVIDIA"),
                DefaultButton = ContentDialogButton.Close,
                Content = $"{ResourceHelper.GetString("General_Success")}: {successCount}\n{ResourceHelper.GetString("General_Failed")}: {failCount}",
                CloseButtonText = ResourceHelper.GetString("General_Close"),
            };

            await completeDialog.ShowAsync();

        }
        else
        {
            downloadingDialog.Hide();
        }
    }

    [RelayCommand]
    async Task DeleteRecordAsync(DLLRecord record)
    {
        if (record.LocalRecord is null)
        {
            Logger.Error("Could not delete record, LocalRecord is null.");
            return;
        }

        var assetTypeName = DLLManager.Instance.GetAssetTypeName(record.AssetType);
        var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("LibraryPage_DeleteDll"),
            PrimaryButtonText = ResourceHelper.GetString("General_Delete"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = ResourceHelper.GetFormattedResourceTemplate("LibraryPage_DeleteDllVersionTemplate", assetTypeName, record.Version),
        };
        var response = await dialog.ShowAsync();
        if (response == ContentDialogResult.Primary)
        {
            var didDelete = record.LocalRecord.Delete();
            if (didDelete)
            {
                if (record.LocalRecord.IsImported)
                {
                    // TODO: What to do here?
                    DLLManager.Instance.DeleteImportedDllRecord(record);
                    await DLLManager.Instance.SaveImportedManifestJsonAsync();

                    // Removing a version changes what is available just as importing one does, so a
                    // game counted as behind this file may not be any more.
                    GameManager.Instance.RefreshUpdateAvailable();
                }
                else
                {
                    record.NotifyPropertyChanged(nameof(record.LocalRecord));
                }
            }
            else
            {
                var errorDialog = new EasyContentDialog(_libraryPage.XamlRoot)
                {
                    Title = ResourceHelper.GetString("General_Error"),
                    CloseButtonText = ResourceHelper.GetString("General_Okay"),
                    DefaultButton = ContentDialogButton.Close,
                    Content = ResourceHelper.GetFormattedResourceTemplate("LibraryPage_UnableToDeleteRecord", assetTypeName),
                };
                await errorDialog.ShowAsync();
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    async Task DownloadRecordAsync(DLLRecord record)
    {
        var result = await record.DownloadAsync();
        if (result.Success is false && result.Cancelled is false)
        {
            var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Error"),
                CloseButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Close,
                Content = result.Message,
            };

            await dialog.ShowAsync();
        }
    }

    [RelayCommand]
    async Task CancelDownloadRecordAsync(DLLRecord record)
    {
        record?.CancelDownload();
        await Task.Delay(10);
    }

    [RelayCommand]
    async Task ExportRecordAsync(DLLRecord dllRecord)
    {
        if (dllRecord.LocalRecord is null)
        {
            return;
        }

        var exportingDialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("LibraryPage_Exporting"),
            // I would like this to be a progress ring but for some reason the ring will not show.
            Content = new ProgressRing()
            {
                IsIndeterminate = true,
            },
        };

        try
        {
            var exportName = $"dlss_swapper_export_{dllRecord.DisplayName.Replace(" ", "_")}.zip";

            var expectedPathDirectory = Path.GetDirectoryName(dllRecord.LocalRecord.ExpectedPath);
            if (string.IsNullOrWhiteSpace(expectedPathDirectory) == false)
            {
                var directoryInfo = new DirectoryInfo(expectedPathDirectory);
                exportName = $"export_{directoryInfo.Name}.zip";
            }

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);

            var fileFilters = new List<FileSystemHelper.FileFilter>()
            {
                new FileSystemHelper.FileFilter("Zip files", "*.zip"),
            };

            var saveFile = FileSystemHelper.SaveFile(hWnd, fileFilters, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), exportName, defaultExtension: "zip");

            if (string.IsNullOrWhiteSpace(saveFile))
            {
                // User cancelled.
                return;
            }

           
            // This will likley not be seen, but keeping it here in case export is very slow (eg. copy over very slow network).
            _ = exportingDialog.ShowAsync();

            // Give UI time to update and show import screen.
            await Task.Delay(50);

            var toExport = new List<(string SourceFileName, string EntryName)>();
            toExport.Add((dllRecord.LocalRecord.ExpectedPath, Path.GetFileName(dllRecord.LocalRecord.ExpectedPath)));

            Exception? exportError = null;
            await Task.Run(() =>
            {
                exportError = ExportDllWorker(saveFile, toExport, null);
            });

            if (exportError is not null)
            {
                throw new Exception("Worker thread failed to export.", exportError);
            }

            exportingDialog.Hide();

            var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Success"),
                CloseButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Close,
                Content = ResourceHelper.GetFormattedResourceTemplate("LibraryPage_ExportedDllTemplate", dllRecord.DisplayName),
            };
            await dialog.ShowAsync();
        }
        catch (Exception err)
        {
            exportingDialog.Hide();
            Logger.Error(err);

            // If the fullExpectedPath does not exist, or there was an error writing it.
            var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("General_Error"),
                CloseButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Close,
                Content = ResourceHelper.GetString("LibraryPage_CouldntExportDll"),
            };
            await dialog.ShowAsync();
        }
    }

    [RelayCommand]
    async Task ShowDownloadErrorAsync(DLLRecord record)
    {
        var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = ResourceHelper.GetString("General_Error"),
            CloseButtonText = ResourceHelper.GetString("General_Okay"),
            Content = record.LocalRecord?.DownloadErrorMessage ?? ResourceHelper.GetString("LibraryPage_CouldntDownload"),
        };
        await dialog.ShowAsync();
    }

    internal void SelectLibrary(GameAssetType gameAssetType)
    {
        var records = DLLManager.Instance.GetRecords(gameAssetType);

        AdvancedCollectionView? newList = null;
        if (records is not null)
        {
            newList = new AdvancedCollectionView(records, true);
        }

        SelectedLibraryList = null;
        SelectedLibraryList = newList;
        OnPropertyChanged(nameof(SelectedLibraryList));

        // Switching engine builds a brand new view, so the filter has to be put back or the search
        // silently stops applying while the box still shows the query. Debug dlls are opt in and
        // that rule now lives in the same predicate — the dll picker always respected the setting
        // and this page never did, so they used to show up here regardless.
        ApplyRecordFilter();

        WatchRecords(records);
        RebuildVersionGroups();
    }

    /// <summary>
    /// Leaves for the games page, narrowed to the games using this exact file.
    /// </summary>
    /// <remarks>
    /// The page could say twelve games were using a file and that was the end of the sentence. The
    /// filter carries its own label so the page it lands on can say what it is showing, which is the
    /// difference between a narrowed library and one that looks broken.
    /// </remarks>
    [RelayCommand]
    void ShowGamesUsing(DLLRecord? dllRecord)
    {
        if (dllRecord is null)
        {
            return;
        }

        var label = DllFilter.LabelFor(
            DLLManager.Instance.GetAssetTypeName(dllRecord.AssetType), dllRecord.DisplayName);

        App.CurrentApp.MainWindow?.ShowGamesUsingDll(
            new DllFilter(dllRecord.AssetType, dllRecord.MD5Hash, dllRecord.Version, label));
    }

    /// <summary>
    /// Copies a dll's hash, which is the only way to tell two builds of one version apart.
    /// </summary>
    [RelayCommand]
    void CopyHash(DLLRecord? dllRecord)
    {
        if (dllRecord is null || string.IsNullOrEmpty(dllRecord.MD5Hash))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(dllRecord.MD5Hash);
        Clipboard.SetContent(package);
    }

    /// <summary>
    /// Shows everything known about one file, and offers to download it when it is not here yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Also what clicking the row does. Both routes exist because the row being clickable is not
    /// discoverable, and a menu item that says what it opens is.
    /// </para>
    /// <para>
    /// The download button matters more than it looks: the row's own Download lives in an overflow
    /// menu, so the most prominent click target on the page - the row - used to land on a dialog
    /// whose only button was a close. Somebody who came to get a version read its hashes and left
    /// no closer to having it. The dialog was also titled with the bare engine name, which is the
    /// one fact the reader already knew; it names the version now.
    /// </para>
    /// </remarks>
    [RelayCommand]
    async Task ShowRecordInfoAsync(DLLRecord? dllRecord)
    {
        if (dllRecord is null)
        {
            return;
        }

        var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
        {
            Title = $"{DLLManager.Instance.GetAssetTypeName(dllRecord.AssetType)} {dllRecord.DisplayName}",
            CloseButtonText = ResourceHelper.GetString("General_Close"),
            DefaultButton = ContentDialogButton.Close,
            Content = new DLLRecordInfoControl(dllRecord),
        };

        // The same rule the row's menu applies: offer a download only for a file that is neither
        // here nor on its way.
        var canDownload = dllRecord.LocalRecord is { IsDownloaded: false, FileDownloader: null };
        if (canDownload)
        {
            dialog.PrimaryButtonText = ResourceHelper.GetString("General_Download");
            dialog.DefaultButton = ContentDialogButton.Primary;
        }

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && canDownload)
        {
            // Fired rather than awaited - the row's progress bar is the download's feedback, and
            // holding this command hostage would keep the row's menu from opening meanwhile.
            _ = DownloadRecordAsync(dllRecord);
        }
    }

    /// <summary>The versions of the selected engine, under one heading per release line.</summary>
    public ObservableCollection<DllVersionGroup> VersionGroups { get; } = new ObservableCollection<DllVersionGroup>();

    INotifyCollectionChanged? watchedRecords;

    /// <summary>
    /// Follows the records for the selected engine so the groups rebuild when they change.
    /// </summary>
    /// <remarks>
    /// The list this replaced was a live view over the master collection, so an import or a
    /// manifest refresh showed up without rebuilding the page. Grouping means holding a snapshot,
    /// and a snapshot nobody refreshes is a page that quietly stops matching what is on disk.
    /// </remarks>
    void WatchRecords(INotifyCollectionChanged? records)
    {
        if (ReferenceEquals(watchedRecords, records))
        {
            return;
        }

        if (watchedRecords is not null)
        {
            watchedRecords.CollectionChanged -= OnRecordsChanged;
        }

        watchedRecords = records;

        if (watchedRecords is not null)
        {
            watchedRecords.CollectionChanged += OnRecordsChanged;
        }
    }

    void OnRecordsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        App.CurrentApp.RunOnUIThread(RebuildVersionGroups);
    }

    void RebuildVersionGroups()
    {
        VersionGroups.Clear();

        if (SelectedLibraryList is null)
        {
            RefreshSearchState(0);
            return;
        }

        // Read from the filtered view, so a debug dll that is being hidden is not counted into a
        // line heading that then has nothing under it.
        var records = SelectedLibraryList.OfType<DLLRecord>().ToList();
        var engineName = DLLManager.Instance.GetAssetTypeName(SelectedAssetType);

        foreach (var group in DllVersionGroup.Build(records, engineName))
        {
            VersionGroups.Add(group);
        }

        RefreshSearchState(records.Count);
    }

    /// <summary>What the page says about a search, and what it says when nothing survived one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyStateVisibility))]
    public partial UpscalersEmptyState? EmptyState { get; set; }

    public Visibility EmptyStateVisibility => EmptyState is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Counted off the rows the page is actually showing, rather than worked out from the filter
    /// again, so the sentence and the emptiness it describes cannot disagree.
    /// </summary>
    void RefreshSearchState(int visibleCount)
    {
        var engineName = DLLManager.Instance.GetAssetTypeName(SelectedAssetType);
        var allRecords = DLLManager.Instance.GetRecords(SelectedAssetType);

        // The engine's own total ignores the search but keeps the debug rule, because that is what
        // "show all versions" would put back.
        var engineTotal = DllSearch.Count(allRecords, null, Settings.Instance.AllowDebugDlls);

        SearchSummary = string.IsNullOrWhiteSpace(SearchText)
            ? string.Empty
            : ResourceHelper.GetFormattedResourceTemplate(
                "Upscalers_SearchSummaryTemplate", visibleCount, engineTotal, engineName);

        var matchesElsewhere = 0;
        if (string.IsNullOrWhiteSpace(SearchText) == false)
        {
            foreach (var dllTypeDefinition in DllTypes.All)
            {
                if (dllTypeDefinition.AssetType == SelectedAssetType)
                {
                    continue;
                }

                matchesElsewhere += DllSearch.Count(
                    DLLManager.Instance.GetRecords(dllTypeDefinition.AssetType),
                    SearchText,
                    Settings.Instance.AllowDebugDlls);
            }
        }

        // Straight off the registry rather than a list kept here, so an engine the manifest does
        // not publish is offered the import it actually needs instead of a refresh that cannot help.
        var importOnly = Dlls.DllTypes.ForAssetType(SelectedAssetType)?.ExpectedInUpstreamManifest == false;

        var state = UpscalersEmptyState.For(visibleCount, engineTotal, engineName, SearchText, matchesElsewhere, importOnly);
        EmptyState = state.Kind == UpscalersEmptyStateKind.None ? null : state;
    }

    /// <summary>Runs whatever the empty state offered to do.</summary>
    [RelayCommand]
    async Task EmptyStatePrimaryAsync()
    {
        if (EmptyState?.Kind == UpscalersEmptyStateKind.NoSearchResults)
        {
            SearchText = string.Empty;
            return;
        }

        // The button says it opens the file picker, so it opens the file picker. Refreshing is
        // what the other empty state offers, and for this engine it would do nothing at all.
        if (EmptyState?.Kind == UpscalersEmptyStateKind.ImportOnly)
        {
            await ImportAsync();
            return;
        }

        await RefreshAsync();
    }

    [RelayCommand]
    async Task DownloadLatestAsync()
    {
        var startedDownloads = 0;
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            var records = DLLManager.Instance.GetRecords(dllTypeDefinition.AssetType);
            if (records is null)
            {
                continue;
            }

            startedDownloads += DownloadLatestRecord(records);
        }

        if (startedDownloads == 0)
        {
            var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("LibraryPage_NoNewDLLs_Title"),
                CloseButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Close,
                Content = ResourceHelper.GetString("LibraryPage_NoNewDLLs_Message"),
            };
            await dialog.ShowAsync();
        }
        else
        {
            var dialog = new EasyContentDialog(_libraryPage.XamlRoot)
            {
                Title = ResourceHelper.GetString("LibraryPage_DownloadsStarted_Title"),
                CloseButtonText = ResourceHelper.GetString("General_Okay"),
                DefaultButton = ContentDialogButton.Close,
                Content = ResourceHelper.GetFormattedResourceTemplate("LibraryPage_DownloadsStarted_Message", startedDownloads),
            };
            await dialog.ShowAsync();
        }
    }

    int DownloadLatestRecord(IReadOnlyList<DLLRecord> records)
    {
        var startedCount = 0;
        var record = GetLatestRecord(records, false);
        if (record?.LocalRecord?.IsDownloaded == false)
        {
            _ = record.DownloadAsync();
            ++startedCount;
        }

        if (Settings.Instance.AllowDebugDlls)
        {
            record = GetLatestRecord(records, true);
            if (record?.LocalRecord?.IsDownloaded == false)
            {
                _ = record.DownloadAsync();
                ++startedCount;
            }
        }

        return startedCount;
    }

    DLLRecord? GetLatestRecord(IReadOnlyList<DLLRecord> records, bool devDllsOnly)
    {
        if (records.Count == 0)
        {
            return null;
        }

        // Ranked by the shared rule so this cannot drift from what the update badge considers
        // newest. It used to compare FSR by its display version as a string, which ranks 3.1.10
        // below 3.1.4.
        DLLRecord? latestRecord = null;
        var latestRank = 0UL;

        foreach (var record in records)
        {
            if (record.IsDevFile != devDllsOnly)
            {
                continue;
            }

            if (DllVersionRanking.TryGetRank(record.AssetType, record.InternalName, record.Version, out var rank) == false)
            {
                continue;
            }

            if (latestRecord is null || rank > latestRank)
            {
                latestRecord = record;
                latestRank = rank;
            }
        }

        return latestRecord;
    }
}
