using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Extensions;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Versioning;

namespace DLSS_Swapper.Data;

internal class DLLManager
{
    public static DLLManager Instance { get; private set; } = new DLLManager();

    /// <summary>
    /// The known records for each swappable dll type, each ordered newest first.
    /// </summary>
    /// <remarks>
    /// Built from the type registry, so a new upscaler gets its collection without anything here
    /// being touched. The collection instances live for the lifetime of the app because the UI binds
    /// to them directly; they are only ever added to and removed from, never replaced.
    /// </remarks>
    readonly Dictionary<GameAssetType, ObservableCollection<DLLRecord>> _records =
        DllTypes.All.ToDictionary(x => x.AssetType, x => new ObservableCollection<DLLRecord>());

    // Named accessors kept so existing bindings and call sites keep working.
    public ObservableCollection<DLLRecord> DLSSRecords => _records[GameAssetType.DLSS];
    public ObservableCollection<DLLRecord> DLSSGRecords => _records[GameAssetType.DLSS_G];
    public ObservableCollection<DLLRecord> DLSSDRecords => _records[GameAssetType.DLSS_D];
    public ObservableCollection<DLLRecord> FSR31DX12Records => _records[GameAssetType.FSR_31_DX12];
    public ObservableCollection<DLLRecord> FSR31VKRecords => _records[GameAssetType.FSR_31_VK];
    public ObservableCollection<DLLRecord> XeSSRecords => _records[GameAssetType.XeSS];
    public ObservableCollection<DLLRecord> XeLLRecords => _records[GameAssetType.XeLL];
    public ObservableCollection<DLLRecord> XeSSFGRecords => _records[GameAssetType.XeSS_FG];
    public ObservableCollection<DLLRecord> XeSSDX11Records => _records[GameAssetType.XeSS_DX11];

    public DllKeyedRecords<HashedKnownDLL> KnownDLLs { get; private set; } = new DllKeyedRecords<HashedKnownDLL>();

    readonly ReaderWriterLockSlim _knownDLLsReadWriterLock = new ReaderWriterLockSlim();

    internal Manifest? Manifest { get; private set; }
    internal Manifest? ImportedManifest { get; private set; }

    public async Task LoadManifestsAsync()
    {
        // Try load the manifest.
        var manifestFile = Storage.GetManifestPath();
        if (File.Exists(manifestFile))
        {
            try
            {
                using (var stream = File.OpenRead(manifestFile))
                {
                    var manifest = await JsonSerializer.DeserializeAsync(stream, SourceGenerationContext.Default.Manifest).ConfigureAwait(false);
                    if (manifest is not null)
                    {
                        Manifest = manifest;
                    }
                }
            }
            catch (Exception err)
            {
                Logger.Error(err);
            }
        }

        // If we could not load the dynamic manifest, try the static one
        if (Manifest is null)
        {
            Logger.Info("No manifest loaded, loading static manifest instead.");
            try
            {
                using (var staticManifestStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DLSS_Swapper.Assets.static_manifest.json"))
                {
                    if (staticManifestStream is not null)
                    {
                        var manifest = await JsonSerializer.DeserializeAsync(staticManifestStream, SourceGenerationContext.Default.Manifest).ConfigureAwait(false);
                        if (manifest is not null)
                        {
                            Logger.Info("Loaded static manifest");
                            Manifest = manifest;
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Logger.Error(err);
            }
        }

        // If we were still unable to load it, it will be loaded in UpdateManifestIfOldAsync.
        // If it isn't loaded there we error out for the user.
        if (Manifest is null)
        {
            Logger.Error("Could not load dynamic or static manifest. Attempting to load remote soon.");
        }

        // Load the imported manifest. If we can't load it we keep it as null. If the file does not exist we don't
        // create a new one as the user may not even be using that feature.
        var importedManifestFile = Storage.GetImportedManifestPath();
        if (File.Exists(importedManifestFile) == true)
        {
            try
            {
                using (var stream = File.OpenRead(importedManifestFile))
                {
                    var importedManifest = await JsonSerializer.DeserializeAsync(stream, SourceGenerationContext.Default.Manifest).ConfigureAwait(false);
                    if (importedManifest is not null)
                    {
                        ImportedManifest = importedManifest;
                    }
                }
            }
            catch (Exception err)
            {
                Logger.Error(err);
            }
        }
        else
        {
            // We don't save the new imported manifest until its actually changed.
            ImportedManifest = new Manifest();
        }

        // If we couldn't load the ImportedManifest we will disable the import system.
        // This helps with preventing overriding of user data.
        if (ImportedManifest is null)
        {
            Logger.Error("Could not load imported manifest, disabling import system.");
        }

        await ProcessManifestsAsync();
    }

    /// <summary>
    /// How often the manifest is re-checked while the app is left open.
    /// </summary>
    /// <remarks>
    /// The manifest is only fetched at startup, so an app left running for days never noticed a new
    /// dll release. A check that finds nothing new costs one request and stops there, because
    /// UpdateManifestAsync compares hashes before doing any work.
    /// </remarks>
    static readonly TimeSpan _periodicManifestCheckInterval = TimeSpan.FromHours(1);

    CancellationTokenSource? _periodicManifestCheckCancellation;

    /// <summary>
    /// Begins re-checking the manifest on an interval. Does nothing if already started.
    /// </summary>
    internal void StartPeriodicManifestCheck()
    {
        if (_periodicManifestCheckCancellation is not null)
        {
            return;
        }

        _periodicManifestCheckCancellation = new CancellationTokenSource();
        _ = RunPeriodicManifestCheckAsync(_periodicManifestCheckCancellation.Token);
    }

    async Task RunPeriodicManifestCheckAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_periodicManifestCheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    // Updates the dll records when something changed, which refreshes which games
                    // are behind. A failure here is not worth telling the user about, it just means
                    // they keep the records they already had until the next check.
                    await UpdateManifestAsync().ConfigureAwait(false);
                }
                catch (Exception err)
                {
                    Logger.Error(err, "Periodic manifest check failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Loads a new manifest from the internet and saves it.
    /// </summary>
    /// <returns>Boolean of if we were able to fetch the manifest from the remote source.</returns>
    internal async Task<bool> UpdateManifestAsync()
    {
        try
        {
            var oldManifestHash = string.Empty;

            var manifestPath = Storage.GetManifestPath();
            if (File.Exists(manifestPath))
            {
                using (var fileStream = File.OpenRead(manifestPath))
                {
                    oldManifestHash = fileStream.GetMD5Hash();
                }
            }

            using (var memoryStream = new MemoryStream())
            {
                // TODO: Check how quickly this takes to timeout if there is no internet connection. Consider
                // adding a "fast UpdateManifest" which will quit early if we were unable to load in 10sec
                // which would then fall back to loading local.
                var fileDownloader = new FileDownloader("https://beeradmoore.github.io/dlss-swapper/manifest.json", 0);
                await fileDownloader.DownloadFileToStreamAsync(memoryStream);

                memoryStream.Position = 0;

                var newManifestHash = memoryStream.GetMD5Hash();

                // If the old manifest on disk is the same as the new one there is no need to do anything as it will already be loaded.
                if (oldManifestHash == newManifestHash)
                {
                    return true;
                }

                memoryStream.Position = 0;

                var manifest = await JsonSerializer.DeserializeAsync(memoryStream, SourceGenerationContext.Default.Manifest);
                if (manifest is null)
                {
                    throw new Exception("Could not deserialize manifest.json.");
                }

                Manifest = manifest;

                try
                {
                    Storage.CreateDirectoryForFileIfNotExists(manifestPath);
                    using (var stream = File.Create(manifestPath))
                    {
                        memoryStream.Position = 0;
                        memoryStream.CopyTo(stream);
                    }
                }
                catch (Exception err)
                {
                    Logger.Error(err);
                    Debugger.Break();
                }

                await ProcessManifestsAsync().ConfigureAwait(false);

                return true;
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
            Debugger.Break();
            return false;
        }
    }

    /// <summary>
    /// Processes manifest and imported manifest objects to the current DLL records lists.
    /// </summary>
    async Task ProcessManifestsAsync()
    {
        // If manifest is not loaded we can't do anything.
        if (Manifest is null)
        {
            return;
        }

        // Update the KnownDLLs list
        _knownDLLsReadWriterLock.EnterWriteLock();
        try
        {
            KnownDLLs = Manifest.KnownDLLs;
        }
        finally
        {
            _knownDLLsReadWriterLock.ExitWriteLock();
        }

        // Cancel downloading of all current DLL records
        foreach (var records in _records.Values)
        {
            CancelDownloads(records);
        }

        // Update incoming DLL record game asset types
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            SetGameAssetType(Manifest.GetRecords(dllTypeDefinition.AssetType), dllTypeDefinition.AssetType);
            SetGameAssetType(ImportedManifest?.GetRecords(dllTypeDefinition.AssetType), dllTypeDefinition.AssetType);
        }

        // Migrate records from zip to raw dlls
        var zipDirectories = Directory.GetDirectories(Storage.GetStorageFolder(), "*_zip", SearchOption.TopDirectoryOnly);
        if (zipDirectories.Length > 0)
        {
            var oldLoadingMessage = App.CurrentApp.MainWindow.ViewModel.LoadingMessage;
            App.CurrentApp.RunOnUIThread(() =>
            {
                App.CurrentApp.MainWindow.ViewModel.LoadingMessage = ResourceHelper.GetString("DllManager_MigratingDlls");
            });

            foreach (var dllTypeDefinition in DllTypes.All)
            {
                CheckDllRecordsForMigration_117(
                    Manifest.GetRecords(dllTypeDefinition.AssetType),
                    ImportedManifest?.GetRecords(dllTypeDefinition.AssetType));
            }

            App.CurrentApp.RunOnUIThread(() =>
            {
                App.CurrentApp.MainWindow.ViewModel.LoadingMessage = oldLoadingMessage;
            });
        }

        // Load local records
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            LoadLocalRecords(Manifest.GetRecords(dllTypeDefinition.AssetType));
            LoadLocalRecords(ImportedManifest?.GetRecords(dllTypeDefinition.AssetType), true);
        }

        // See if there is any imported manifest items that are to be migrated to downloaded
        // CheckImportedManifestForCleanUp needs to be called after LoadLocalRecords
        var didChangeImportedManifest = false;
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            didChangeImportedManifest |= CheckImportedManifestForCleanUp(
                Manifest.GetRecords(dllTypeDefinition.AssetType),
                ImportedManifest?.GetRecords(dllTypeDefinition.AssetType));
        }

        if (didChangeImportedManifest == true)
        {
            await SaveImportedManifestJsonAsync().ConfigureAwait(false);
        }

        App.CurrentApp.RunOnUIThread(() =>
        {
            // Merge each of the manifests into the master DLL record list
            foreach (var (assetType, records) in _records)
            {
                MergeManifestsIntoMasterList(records, Manifest.GetRecords(assetType), ImportedManifest?.GetRecords(assetType));
            }

            // Now that we know what versions exist, work out which games are behind.
            GameManager.Instance.RefreshUpdateAvailable();
        });
    }

    static void CancelDownloads(ObservableCollection<DLLRecord> dllRecords)
    {
        foreach (var dllRecord in dllRecords)
        {
            dllRecord.CancelDownload();
        }
    }

    /// <summary>
    /// Updates every dllRecord to have the specific gameAssetType
    /// </summary>
    /// <param name="dllRecords"></param>
    /// <param name="gameAssetType"></param>
    static void SetGameAssetType(List<DLLRecord>? dllRecords, GameAssetType gameAssetType)
    {
        // Null when there is no imported manifest, or when a manifest predates this asset type.
        if (dllRecords is null)
        {
            return;
        }

        foreach (var dllRecord in dllRecords)
        {
            dllRecord.AssetType = gameAssetType;
        }
    }

    /// <summary>
    /// Looks through each DllRecord and see if they need to be migrated to new folder structure in v1.1.7
    ///
    /// This needs to be called before LoadLocalRecords
    /// </summary>
    /// <param name="dllRecords"></param>
    /// <param name="importedDllRecords"></param>
    /// <returns></returns>
    static void CheckDllRecordsForMigration_117(List<DLLRecord>? dllRecords, List<DLLRecord>? importedDllRecords)
    {
        if (dllRecords is null)
        {
            return;
        }

        // The list and the flag come out of the same tuple, so they cannot disagree. They were two
        // near identical loops differing only in that bool, and the second one iterated dllRecords
        // while passing isImported: true - so a dll that exists only in the imported manifest was
        // never migrated to the v1.1.7 layout. Its file was then not found at the new path,
        // IsDownloaded stayed false, the cleanup read that as "the file is gone" and removed the
        // record, and the dll disappeared from the library on first launch after upgrading with
        // nothing said. Only genuinely custom dlls were reachable, which are the irreplaceable ones.
        foreach (var (records, isImported) in new[] { (dllRecords, false), (importedDllRecords, true) })
        {
            if (records is null)
            {
                continue;
            }

            foreach (var dllRecord in records)
            {
                CheckDllRecordForMigration_117(dllRecord, isImported);
            }
        }
    }

    /// <summary>
    /// As of v1.1.7 we migrated DLLs from being in a zip folder to being a DLL in a folder.
    /// This method will move where the zip was to where the dll will be.
    /// </summary>
    /// <param name="dllRecord"></param>
    /// <param name="isImported"></param>
    static void CheckDllRecordForMigration_117(DLLRecord dllRecord, bool isImported)
    {
        // From GetExpectedZipPath
        var recordType = dllRecord.GetRecordSimpleType();
        if (recordType == string.Empty)
        {
            return;
        }

        var zipPath = Path.Combine(Storage.GetStorageFolder(), (isImported ? $"imported_{recordType}_zip" : $"{recordType}_zip"));
        if (string.IsNullOrWhiteSpace(zipPath))
        {
            return;
        }

        // If the zip path does not exist then we don't need to continue any further.
        if (Directory.Exists(zipPath) == false)
        {
            return;
        }

        var legacyExpectedPath = Path.Combine(zipPath, $"{dllRecord.Version}_{dllRecord.MD5Hash}.zip");
        if (File.Exists(legacyExpectedPath) == false)
        {
            return;
        }

        var dllPath = GetExpectedDllFileName(dllRecord, isImported);
        if (string.IsNullOrWhiteSpace(dllPath))
        {
            return;
        }


        var dllName = Path.GetFileName(dllPath);
        if (string.IsNullOrWhiteSpace(dllName))
        {
            return;
        }

        Storage.CreateDirectoryForFileIfNotExists(dllPath);

        var didExtract = false;

        try
        {
            using (var fileStream = File.OpenRead(legacyExpectedPath))
            {
                using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read, true))
                {
                    var dllEntry = zipArchive.Entries.Single(x => x.Name.Equals(dllName, StringComparison.OrdinalIgnoreCase));
                    dllEntry.ExtractToFile(dllPath, true);
                    didExtract = true;
                }
            }
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Could not extract {legacyExpectedPath} to {dllPath}.");
        }

        if (didExtract == true)
        {
            try
            {
                // Delete the zip we moved
                File.Delete(legacyExpectedPath);
            }
            catch (Exception err)
            {
                Logger.Error(err, $"Could not delete {legacyExpectedPath}");
            }

            // If the old zip father is empty we can delete it.
            if (Directory.GetFiles(zipPath).Length == 0 && Directory.GetDirectories(zipPath).Length == 0)
            {
                try
                {
                    Directory.Delete(zipPath);
                }
                catch (Exception err)
                {
                    Logger.Error(err, $"Could not delete {zipPath}");
                }
            }
        }
    }

    /// <summary>
    /// Looks through each of the imported DLL records to see if they:
    /// - Need to be deleted because the file no longer exists
    /// - Need to be migrated from imported to standard manifest
    ///
    /// This needs to be called after LoadLocalRecords
    /// </summary>
    /// <param name="dllRecords"></param>
    /// <param name="importedDllRecords"></param>
    /// <returns></returns>
    static bool CheckImportedManifestForCleanUp(List<DLLRecord>? dllRecords, List<DLLRecord>? importedDllRecords)
    {
        var didChangeImportedManifestList = false;

        if (dllRecords is not null && importedDllRecords is not null)
        {
            var importedDllRecordsToDelete = new List<DLLRecord>();

            // Delete imported DLLs if the file is no longer found.
            foreach (var importedDllRecord in importedDllRecords)
            {
                // If IsDownloaded is false it means the DLL does not exist on the disk
                if (importedDllRecord.LocalRecord?.IsDownloaded == false)
                {
                    Logger.Info($"Imported file not found ({importedDllRecord.LocalRecord}), deleting imported record.");
                    importedDllRecordsToDelete.Add(importedDllRecord);
                }
            }

            // Check if imported DLLs are in the new manifest. If they are we want to
            // move them and pretend they were imported.
            foreach (var importedDllRecord in importedDllRecords)
            {
                // Skip the imported DLL if we are about to remove it.
                if (importedDllRecordsToDelete.Contains(importedDllRecord))
                {
                    continue;
                }

                var manifestDllRecord = dllRecords.FirstOrDefault(x => x.MD5Hash == importedDllRecord.MD5Hash);

                // Make sure both records have a local record.
                if (manifestDllRecord?.LocalRecord is not null && importedDllRecord.LocalRecord is not null)
                {
                    try
                    {
                        // If the DLL is downloaded there is nothing else to change here. Delete the imported one.
                        if (manifestDllRecord.LocalRecord.IsDownloaded == true)
                        {
                            importedDllRecordsToDelete.Add(importedDllRecord);
                            continue;
                        }

                        var oldZipPath = importedDllRecord.LocalRecord.ExpectedPath;
                        if (File.Exists(oldZipPath) == false)
                        {
                            // This should never happen.
                            Logger.Error($"oldZipPath ({oldZipPath}) does not exist.");
                            Debugger.Break();
                            continue;
                        }

                        var expectedPath = Path.GetDirectoryName(manifestDllRecord.LocalRecord.ExpectedPath);
                        if (string.IsNullOrWhiteSpace(expectedPath))
                        {
                            continue;
                        }

                        if (Directory.Exists(expectedPath) == false)
                        {
                            Directory.CreateDirectory(expectedPath);
                        }

                        File.Move(importedDllRecord.LocalRecord.ExpectedPath, manifestDllRecord.LocalRecord.ExpectedPath);

                        App.CurrentApp.RunOnUIThread(() =>
                        {
                            manifestDllRecord.LocalRecord.IsDownloaded = true;
                        });

                        importedDllRecordsToDelete.Add(importedDllRecord);
                        Logger.Info($"Moving imported record to be local record, {importedDllRecord.LocalRecord.ExpectedPath} -> {manifestDllRecord.LocalRecord.ExpectedPath}");
                    }
                    catch (Exception err)
                    {
                        Logger.Error(err);
                        Debugger.Break();
                    }
                }
            }


            // If any of the imported DLLs need to be removed from the imported DLL list.
            if (importedDllRecordsToDelete.Count > 0)
            {
                foreach (var dllRecord in importedDllRecordsToDelete)
                {
                    var dllRecordPath = dllRecord.LocalRecord?.ExpectedPath;
                    // == false. The condition was inverted - "path is empty AND the file at the
                    // empty path exists" - which no input satisfies, so the cleanup this loop
                    // exists for never deleted a single file and removed records left their zips
                    // behind as unreachable orphans.
                    if (string.IsNullOrWhiteSpace(dllRecordPath) == false && File.Exists(dllRecordPath))
                    {
                        try
                        {
                            File.Delete(dllRecordPath);
                        }
                        catch (Exception err)
                        {
                            Logger.Error(err, $"Could not delete {dllRecordPath}");
                        }
                    }

                    importedDllRecords.Remove(dllRecord);
                }

                didChangeImportedManifestList = true;
            }
        }

        return didChangeImportedManifestList;
    }

    /// <summary>
    /// Loads the LocalRecrod object on every dllRecord in the list.
    /// </summary>
    /// <param name="dllRecords"></param>
    void LoadLocalRecords(List<DLLRecord>? dllRecords, bool isImported = false)
    {
        if (dllRecords is null)
        {
            return;
        }

        foreach (var dllRecord in dllRecords)
        {
            LoadLocalRecord(dllRecord, isImported);
        }
    }

    void LoadLocalRecord(DLLRecord dllRecord, bool isImported)
    {
        // If we are loading a new LocalRecord we should cancel existing download.
        dllRecord.CancelDownload();

        // Null out the existing record so we can tell if loading failed.
        App.CurrentApp.RunOnUIThread(() =>
        {
            dllRecord.LocalRecord = null;
        });

        var expectedPath = GetExpectedDllFileName(dllRecord, isImported);
        if (string.IsNullOrWhiteSpace(expectedPath))
        {
            return;
        }

        var localRecord = LocalRecord.FromExpectedPath(expectedPath, isImported);
        App.CurrentApp.RunOnUIThread(() =>
        {
            dllRecord.LocalRecord = localRecord;
        });
    }


    /// <summary>
    /// Takes DLL list from manifest and imported manifest and inserts them into the master DLL records list which is bindable in the app.
    /// </summary>
    /// <param name="records"></param>
    /// <param name="manifestRecords"></param>
    /// <param name="importedRecords"></param>
    /// <returns>Returns true if importedRecords was changed and requires saving</returns>
    static void MergeManifestsIntoMasterList(ObservableCollection<DLLRecord> records, List<DLLRecord>? manifestRecords, List<DLLRecord>? importedManifestRecords)
    {
        if (manifestRecords is null)
        {
            return;
        }

        // Sort the lists first to ensure local sort, not remote sort.
        manifestRecords.Sort();
        importedManifestRecords?.Sort();

        var tempRecords = new List<DLLRecord>(records);

        foreach (var dllRecord in manifestRecords)
        {
            // LoadLocalRecord(dllRecord, false);

            var insertIndex = tempRecords.BinarySearch(dllRecord);
            if (insertIndex < 0) // InsertObject
            {
                insertIndex = ~insertIndex;


                records.Insert(insertIndex, dllRecord);

                tempRecords.Insert(insertIndex, dllRecord);
            }
            else // Update object
            {
                records[insertIndex].CopyFrom(dllRecord);
                tempRecords[insertIndex] = dllRecord;
            }
        }

        // Now that we have loaded DLL records we want to add the importedRecords back into that list.
        if (importedManifestRecords?.Any() == true)
        {
            foreach (var importedRecord in importedManifestRecords)
            {
                var insertIndex = tempRecords.BinarySearch(importedRecord);
                if (insertIndex < 0)
                {
                    insertIndex = ~insertIndex;
                    records.Insert(insertIndex, importedRecord);
                    tempRecords.Insert(insertIndex, importedRecord);
                }
                else
                {
                    records[insertIndex].CopyFrom(importedRecord);
                    tempRecords[insertIndex] = importedRecord;
                }
            }
        }

    }

    internal bool HasLoadedManifest()
    {
        return Manifest is not null;
    }

    internal bool HasLoadedImportedManifest()
    {
        return ImportedManifest is not null;
    }

    internal async Task<bool> SaveImportedManifestJsonAsync()
    {
        if (ImportedManifest is null)
        {
            Logger.Error("Could not save imported manifest as importing system is disabled.");
            return false;
        }

        var importedManifestFile = Storage.GetImportedManifestPath();

        // Built beside the file and moved over it. This is the only index of which imported dll is
        // which, and the load path deliberately refuses to overwrite a file it could not read - so a
        // write interrupted part way through used to disable importing permanently, with the dlls
        // still on disk and nothing left saying what any of them were.
        return await Storage.WriteFileAtomicallyAsync(importedManifestFile, async stream =>
        {
            await JsonSerializer.SerializeAsync(stream, ImportedManifest, SourceGenerationContext.Default.Manifest).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    static string GetExpectedDllFileName(DLLRecord dllRecord, bool isImported)
    {
        var dllPath = GetExpectedDllPath(dllRecord, isImported);
        if (string.IsNullOrWhiteSpace(dllPath))
        {
            return string.Empty;
        }

        var dllName = DllNameForGameAssetType(dllRecord.AssetType);
        if (string.IsNullOrWhiteSpace(dllName))
        {
            return string.Empty;
        }

        return Path.Combine(dllPath, dllName);

    }
    static string GetExpectedDllPath(DLLRecord dllRecord, bool isImported)
    {
        var recordType = dllRecord.GetRecordSimpleType();

        var dllsPath = Path.Combine(Storage.GetStorageFolder(), "dlls", (isImported ? $"imported" : string.Empty), recordType);
        if (string.IsNullOrWhiteSpace(dllsPath))
        {
            return string.Empty;
        }

        var individualDllPath = Path.Combine(dllsPath, $"{recordType}_v{dllRecord.Version}_{dllRecord.MD5Hash}");
        if (string.IsNullOrWhiteSpace(individualDllPath))
        {
            return string.Empty;
        }

        return individualDllPath;
    }

    public string GetAssetTypeName(GameAssetType assetType)
    {
        var definition = DllTypes.ForAssetType(assetType) ?? throw new Exception($"Unknown AssetType: {assetType}");
        return ResourceHelper.GetString(definition.DisplayNameResourceKey);
    }


    public GameAssetType GetAssetBackupType(GameAssetType assetType)
    {
        var definition = DllTypes.ForAssetType(assetType) ?? throw new Exception($"Unknown AssetType: {assetType}");
        return definition.BackupAssetType;
    }

    /// <summary>
    /// Which vendor an asset type belongs to.
    /// </summary>
    public DllVendor GetAssetVendor(GameAssetType assetType)
    {
        return DllTypes.ForAssetType(assetType)?.Vendor ?? DllVendor.Unknown;
    }

    /// <summary>
    /// Short technology name for a vendor, used on the update badge.
    /// </summary>
    /// <remarks>Product names, so they are not translated.</remarks>
    public string GetVendorShortName(DllVendor vendor)
    {
        return vendor switch
        {
            DllVendor.Nvidia => "DLSS",
            DllVendor.Amd => "FSR",
            DllVendor.Intel => "XeSS",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Every record we know of for an asset type, ordered newest first.
    /// </summary>
    /// <returns>Null for asset types we don't offer swaps for, such as the backup types.</returns>
    public ObservableCollection<DLLRecord>? GetRecords(GameAssetType assetType)
    {
        return _records.TryGetValue(assetType, out var records) ? records : null;
    }

    /// <summary>
    /// The newest record we would recommend for an asset type.
    /// </summary>
    /// <remarks>
    /// Records are already sorted newest first. Dev builds are skipped because they are debug
    /// versions, so telling someone an update is available and pointing them at one would be wrong.
    /// </remarks>
    public DLLRecord? GetLatestRecord(GameAssetType assetType)
    {
        var records = GetRecords(assetType);
        if (records is null)
        {
            return null;
        }

        // Ranked explicitly rather than taken from the collection's order. The order sorts FSR by
        // its internal name as a string, which puts 3.1.4 above 3.1.10.
        DLLRecord? latestRecord = null;
        var latestRank = 0UL;

        foreach (var record in records)
        {
            if (record.IsDevFile)
            {
                continue;
            }

            if (DllVersionRanking.TryGetRank(assetType, record.InternalName, record.Version, out var rank) == false)
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

    /// <summary>
    /// Checks to see if the current GameAsset DLL is known to already existing DLL record known GameAsset for a game in a particular library
    /// </summary>
    /// <param name="gameAsset"></param>
    /// <param name="game"></param>
    /// <returns></returns>
    ///
    public bool IsInKnownGameAsset(GameAsset gameAsset, Game game)
    {
        // A backup resolves to the same definition as the dll it backs up. A backup of a dll we
        // recognise is just as recognised.
        var dllTypeDefinition = DllTypes.ForAssetTypeIncludingBackup(gameAsset.AssetType);
        if (dllTypeDefinition is null)
        {
            return false;
        }

        // First check if it is in the DLSS Swapper manifest.
        var records = GetRecords(dllTypeDefinition.AssetType);
        if (records?.Any(x => gameAsset.Hash.Equals(x.MD5Hash, StringComparison.InvariantCultureIgnoreCase)) == true)
        {
            return true;
        }

        // Otherwise it may be a dll the game shipped with, which we track separately per game.
        HashedKnownDLL? hashedKnownDLL = null;
        _knownDLLsReadWriterLock.EnterReadLock();
        try
        {
            hashedKnownDLL = KnownDLLs.GetRecords(dllTypeDefinition.AssetType)?
                .FirstOrDefault(x => gameAsset.Hash.Equals(x.Hash, StringComparison.InvariantCultureIgnoreCase));
        }
        finally
        {
            _knownDLLsReadWriterLock.ExitReadLock();
        }

        if (hashedKnownDLL is null)
        {
            return false;
        }

        if (hashedKnownDLL.Sources.TryGetValue(game.GameLibrary.ToString(), out var gameHashes) == true)
        {
            return gameHashes.Contains(game.TitleBase64);
        }

        return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filePath">Path of the DLL you wish to import.</param>
    /// <param name="zippedDllFullName"></param>
    /// <param name="overrideFileName">Override the filename for importing NGX models that are not .dlls yet.</param>
    /// <returns></returns>

    internal DLLImportResult ImportDll(string filePath, string? zippedDllFullName = null, string? overrideFileName = null)
    {
        if (ImportedManifest is null)
        {
            return DLLImportResult.FromFail(zippedDllFullName ?? filePath, ResourceHelper.GetString("DllManager_ImportFeatureDisabled"));
        }

        var fileName = overrideFileName ?? Path.GetFileName(filePath);

        ObservableCollection<DLLRecord>? recordList = null;
        List<DLLRecord>? importedRecordList = null;
        GameAssetType? gameAssetType = null;

        var dllTypeDefinition = DllTypes.ForFileName(fileName);
        if (dllTypeDefinition is not null)
        {
            gameAssetType = dllTypeDefinition.AssetType;
            recordList = GetRecords(dllTypeDefinition.AssetType);
            importedRecordList = ImportedManifest.GetRecords(dllTypeDefinition.AssetType);
        }

        if (gameAssetType is null || recordList is null || importedRecordList is null)
        {
            return DLLImportResult.FromFail(zippedDllFullName ?? filePath, ResourceHelper.GetString("DllManager_UnknownTypeDll"));
        }

        var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
        var isTrusted = WinTrust.VerifyEmbeddedSignature(filePath);

        // Don't do anything with untrusted dlls.
        if (Settings.Instance.AllowUntrusted == false && isTrusted == false)
        {
            return DLLImportResult.FromFail(zippedDllFullName ?? filePath, ResourceHelper.GetString("DllManager_UntrustedDll"));
        }

        var dllHash = versionInfo.GetMD5Hash();

        var importingAsDownloadedDll = false;

        // We only need to check recordList and not importedRecordList as imported DLLs are in both lists.
        var existingDll = recordList.FirstOrDefault(x => string.Equals(x.MD5Hash, dllHash, StringComparison.InvariantCultureIgnoreCase));
        if (existingDll is not null)
        {
            // If the DLL is already imported we can skip it.
            if (existingDll.LocalRecord?.IsDownloaded == true)
            {
                return DLLImportResult.FromSucces(zippedDllFullName ?? filePath, $"{fileName} {ResourceHelper.GetString("DllManager_AlreadyImported")}", false);
            }
            importingAsDownloadedDll = true;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var dllRecord = existingDll ?? new DLLRecord()
            {
                Version = versionInfo.GetFormattedFileVersion(),
                VersionNumber = versionInfo.GetFileVersionNumber(),
                MD5Hash = dllHash,
                FileSize = fileInfo.Length,
                ZipFileSize = 0,
                ZipMD5Hash = string.Empty,
                IsSignatureValid = isTrusted,
                AssetType = gameAssetType.Value,
            };


            // TODO: Get extra data from DLL if possible

            var expectedPath = GetExpectedDllFileName(dllRecord, !importingAsDownloadedDll);
            if (string.IsNullOrWhiteSpace(expectedPath))
            {
                return DLLImportResult.FromFail(zippedDllFullName ?? filePath, "Could not import DLL.");
            }
            Storage.CreateDirectoryForFileIfNotExists(expectedPath);

            // Move new record to where it should live
            File.Copy(filePath, expectedPath, true);
            var newLocalRecord = LocalRecord.FromExpectedPath(expectedPath, !importingAsDownloadedDll);

            App.CurrentApp.RunOnUIThread(() =>
            {
                dllRecord.LocalRecord = null;
                dllRecord.LocalRecord = newLocalRecord;
            });

            // Add our new record.
            if (importingAsDownloadedDll == true)
            {
                // NOOP - DLL is already in the list, we just updated the LocalRecord for it.
            }
            else
            {
                // Insert into the main DLL list
                var tempList = new List<DLLRecord>(recordList);
                var insertIndex = tempList.BinarySearch(dllRecord);
                if (insertIndex < 0)
                {
                    insertIndex = ~insertIndex;
                }
                App.CurrentApp.RunOnUIThread(() =>
                {
                    recordList.Insert(insertIndex, dllRecord);
                });

                // Insert into the list used for local manifest
                var importedInsertIndex = importedRecordList.BinarySearch(dllRecord);
                if (importedInsertIndex < 0)
                {
                    importedInsertIndex = ~importedInsertIndex;
                }
                importedRecordList.Insert(importedInsertIndex, dllRecord);
            }

            return DLLImportResult.FromSucces(zippedDllFullName ?? filePath, fileName, importingAsDownloadedDll);
        }
        catch (Exception err)
        {
            Logger.Error(err);
            return DLLImportResult.FromFail(zippedDllFullName ?? filePath, err.Message);
        }
    }

    internal void DeleteImportedDllRecord(DLLRecord dllRecord)
    {
        ObservableCollection<DLLRecord>? recordList = null;
        List<DLLRecord>? importedRecordList = null;

        var dllTypeDefinition = DllTypes.ForAssetType(dllRecord.AssetType);
        if (dllTypeDefinition is not null)
        {
            recordList = GetRecords(dllTypeDefinition.AssetType);
            importedRecordList = ImportedManifest?.GetRecords(dllTypeDefinition.AssetType);
        }

        if (recordList is null)
        {
            // For some reason we couldn't get the recordList, is this a new DLL type?
            Debugger.Break();
            return;
        }

        recordList.Remove(dllRecord);
        importedRecordList?.Remove(dllRecord);
    }

    internal static string DllNameForGameAssetType(GameAssetType gameAssetType)
    {
        return DllTypes.ForAssetType(gameAssetType)?.FileName ?? string.Empty;
    }

    /// <summary>
    /// This handles extracting of the DLL from both downloaded and imported zips (when imported matches the hash of one that could be downloaded)
    /// </summary>
    /// <param name="zipArchive"></param>
    /// <param name="dllRecord"></param>
    /// <exception cref="Exception"></exception>
    internal static void HandleExtractFromZip(ZipArchive zipArchive, DLLRecord dllRecord)
    {
        if (dllRecord.LocalRecord is null)
        {
            throw new Exception("LocalRecord was null when attempting to extract dll from zip.");
        }

        var dllName = DLLManager.DllNameForGameAssetType(dllRecord.AssetType);
        var entry = zipArchive.Entries.Single(x => x.Name.Equals(dllName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            throw new Exception("Could not find dll in zip.");
        }
        else
        {
            Storage.CreateDirectoryForFileIfNotExists(dllRecord.LocalRecord.ExpectedPath);
            entry.ExtractToFile(dllRecord.LocalRecord.ExpectedPath, true);
        }
    }

}
