using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Extensions;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using DLSS_Swapper.Swapping;
using DLSS_Swapper.UserControls;
using DLSS_Swapper.Versioning;
using Microsoft.UI.Xaml.Controls;
using NvAPIWrapper.DRS;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data;

public abstract partial class Game : ObservableObject, IComparable<Game>, IEquatable<Game> //, INotifyPropertyChanged
{
    [PrimaryKey]
    [Column("id")]
    public string ID { get; set; } = string.Empty;

    [Column("platform_id")]
    public string PlatformId { get; set; } = string.Empty;

    [ObservableProperty]
    [Column("title")]
    public partial string Title { get; set; } = string.Empty;

    // Used to cache the title as a base64 string
    string? _titleBase64;
    [Ignore]
    public string TitleBase64 => _titleBase64 ??= Convert.ToBase64String(Encoding.UTF8.GetBytes(Title));

    [Column("install_path")]
    public string InstallPath { get; set; } = string.Empty;

    [ObservableProperty]
    [Column("cover_image")]
    public partial string? CoverImage { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial uint? DlssPreset { get; set; }

    [ObservableProperty]
    [Ignore]
    public partial uint? DlssDPreset { get; set; }


    [ObservableProperty]
    [Ignore]
    public partial uint? DlssGPreset { get; set; }

    [Ignore]
    public DriverSettingsProfile? DriverSettingsProfile { get; set; }

    /*
    [ObservableProperty]
    [property: Column("base_dlss_version")]
    string baseDLSSVersion = string.Empty;

    [ObservableProperty]
    [property: Column("current_dlss_version")]
    string currentDLSSVersion = string.Empty;

    [ObservableProperty]
    [property: Column("current_dlss_hash")]
    string currentDLSSHash = string.Empty;

    [ObservableProperty]
    [property: Column("base_dlss_hash")]
    string baseDLSSHash = string.Empty;

    [ObservableProperty]
    [property: Column("has_dlss")]
    bool hasDLSS = false;
    */

    [ObservableProperty]
    [Column("has_swappable_items")]
    public partial bool HasSwappableItems { get; set; } = false;

    [ObservableProperty]
    [Column("notes")]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    [Column("is_favourite")]
    public partial bool IsFavourite { get; set; } = false;

    /// <summary>
    /// If the game is hidden from the main list or not. All hidden games are still processed.
    /// If the value is null the user has not set the value and this should be considered as not hidden.
    /// </summary>
    [ObservableProperty]
    [Column("is_hidden")]
    public partial bool? IsHidden { get; set; } = null;

    [ObservableProperty]
    [Ignore]
    public partial bool Processing { get; set; } = false;

    [Ignore]
    public abstract GameLibrary GameLibrary { get; }

    [Ignore]
    //public string ExpectedCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_600_900.jpg");
    //public string ExpectedCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_600_900.png");
    public string ExpectedCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_400_600.png");
    //public string ExpectedCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_600_900.webp");

    [Ignore]
    //public string ExpectedCustomCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_custom_600_900.jpg");
    //public string ExpectedCustomCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_custom_600_900.png");
    public string ExpectedCustomCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_custom_400_600.png");
    //public string ExpectedCustomCoverImage => Path.Combine(Storage.GetImageCachePath(), $"{ID}_custom_600_900.webp");

    [Ignore]
    public List<GameAsset> GameAssets { get; } = new List<GameAsset>();

    [Ignore]
    public bool NeedsProcessing { get; set; } = false;

    bool _isLoadingCoverImage;

    /// <summary>
    /// Convenience accessor for the game grid, which deliberately shows only DLSS.
    /// </summary>
    [Ignore]
    public GameAssetSlot? DlssSlot => GetAssetSlot(GameAssetType.DLSS);

    /// <summary>True when any dll installed in this game has a newer version available to swap to.</summary>
    [ObservableProperty]
    [Ignore]
    public partial bool UpdateAvailable { get; set; } = false;

    /// <summary>One entry per vendor that has an out of date dll in this game. Empty when nothing is.</summary>
    [ObservableProperty]
    [Ignore]
    public partial List<DllVendorUpdate> AvailableUpdates { get; set; } = new List<DllVendorUpdate>();

    /// <summary>
    /// The dll types with a newer version available, which is what "update all" acts on.
    /// </summary>
    /// <remarks>Kept alongside the badges so both come from the same pass.</remarks>
    [ObservableProperty]
    [Ignore]
    public partial IReadOnlyList<GameAssetType> OutdatedAssetTypes { get; set; } = [];

    /// <summary>
    /// What this game has installed for each swappable dll type.
    /// </summary>
    /// <remarks>
    /// One slot per type, created once and never replaced, so anything bound to a slot stays bound
    /// for the life of the game.
    /// </remarks>
    readonly List<GameAssetSlot> _assetSlots = DllTypes.All
        .Select(x => new GameAssetSlot() { AssetType = x.AssetType })
        .ToList();

    [Ignore]
    public IReadOnlyList<GameAssetSlot> AssetSlots => _assetSlots;

    /// <summary>The slot for an asset type, or null if it is not a swappable one.</summary>
    public GameAssetSlot? GetAssetSlot(GameAssetType assetType)
    {
        return _assetSlots.FirstOrDefault(x => x.AssetType == assetType);
    }



    [Ignore]
    public abstract bool IsReadyToPlay { get; }

    protected void SetID()
    {
        // Seeing as we use ID, it sure would be a shame if a PlatformId was set to "C:\Program Files\"
        // So try to remove all funky characters before

        var platformId = PlatformId;
        foreach (var invalidPathChar in PathHelpers.InvalidFileNamePathChars)
        {
            if (platformId.Contains(invalidPathChar))
            {
                platformId = platformId.Replace(invalidPathChar, '_');
            }
        }

        ID = GameLibrary switch
        {
            GameLibrary.Steam => $"steam_{platformId}",
            GameLibrary.GOG => $"gog_{platformId}",
            GameLibrary.EpicGamesStore => $"epicgamesstore_{platformId}",
            GameLibrary.UbisoftConnect => $"ubisoftconnect_{platformId}",
            GameLibrary.XboxApp => $"xboxapp_{platformId}",
            GameLibrary.ManuallyAdded => $"manuallyadded_{platformId}",
            GameLibrary.BattleNet => $"battlenet_{platformId}",
            GameLibrary.EAApp => $"eaapp_{platformId}",
            _ => throw new Exception($"Unknown GameLibrary {GameLibrary} while setting ID"),
        };
    }

    /// <summary>
    /// Detects DLSS and updates cover image.
    /// </summary>
    public void ProcessGame(bool autoSave = true, bool forceNeedsProcessing = false)
    {
        // If we are alreayd procssing we don't need to process again
        if (Processing == true)
        {
            return;
        }

        App.CurrentApp.RunOnUIThread(() =>
        {
            NeedsProcessing = false;
        });

        if (string.IsNullOrEmpty(InstallPath))
        {
            return;
        }

        if (Directory.Exists(InstallPath) == false)
        {
            return;
        }

        App.CurrentApp.RunOnUIThread(() =>
        {
            Processing = true;
            HasSwappableItems = false;
        });

        ThreadPool.QueueUserWorkItem(async (stateInfo) =>
        {
            var newHasSwappableItems = false;

            try
            {
                var shouldUpdatedCover = true;

                if (forceNeedsProcessing == true && File.Exists(ExpectedCustomCoverImage) == false)
                {
                    // If we are forcing game load and custom cover image doesnt exist we will force load the cover no matter what.
                }
                else
                {
                    // This shouldn't crash, bit if it does lets not take down the entire processing.
                    try
                    {
                        FileInfo? fileInfo = null;
                        if (File.Exists(ExpectedCustomCoverImage))
                        {
                            // If we are using a custom cover we don't want to try reloading any cover so we don't set fileInfo.
                            shouldUpdatedCover = false;
                        }
                        else if (File.Exists(ExpectedCoverImage))
                        {
                            fileInfo = new FileInfo(ExpectedCoverImage);
                        }

                        if (fileInfo is not null)
                        {
                            var daysSinceLastModified = (DateTime.Now - fileInfo.LastWriteTime).TotalDays;

                            // Add +/- 2 days so not all will process at the same time.
                            daysSinceLastModified += ((new Random()).NextDouble() - 0.5) * 4.0;

                            // If its less than 7 days lets not try refresh.
                            if (daysSinceLastModified < 7)
                            {
                                shouldUpdatedCover = false;
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        Logger.Error(err);
                        Debugger.Break();
                    }
                }

                Task? coverImageTask = null;
                if (shouldUpdatedCover)
                {
                    coverImageTask = UpdateCacheImageAsync();
                }
                else
                {
                    Logger.Verbose($"Skipping updating cover for {Title}");
                }

                var enumerationOptions = new EnumerationOptions();
                enumerationOptions.RecurseSubdirectories = true;
                enumerationOptions.AttributesToSkip |= FileAttributes.ReparsePoint;

                var oldGameAssets = GameAssets.ToList();
                GameAssets.Clear();
                using (await Database.Instance.Mutex.LockAsync())
                {
                    await Database.Instance.Connection.ExecuteAsync("DELETE FROM game_asset WHERE id = ?", ID).ConfigureAwait(false);
                }
                // TODO: See if changing these to filter specific files, or getting very *.dll and looking for our specific ones is faster
                var dllPaths = Directory.GetFiles(InstallPath, "*.dll", enumerationOptions);

                /*
                var dlssDllPaths = Directory.GetFiles(InstallPath, "nvngx_dlss.dll", enumerationOptions);
                var dlssgDllPaths = Directory.GetFiles(InstallPath, "nvngx_dlssg.dll", enumerationOptions);
                var dlssdDllPaths = Directory.GetFiles(InstallPath, "nvngx_dlssd.dll", enumerationOptions);
                var xessDllPaths = Directory.GetFiles(InstallPath, "libxess.dll", enumerationOptions);
                */

                var dllHistory = new List<GameHistory>();
                var unknownGameAssets = new List<GameAsset>();

                // We have never recorded a dll for this game, so whatever is here now is what the
                // game shipped with. That is the only moment we can be sure of it, which is why
                // backing up happens here and not on every scan.
                var isFirstTimeSeeingThisGame = Settings.Instance.BackupNewGamesAutomatically && oldGameAssets.Count == 0;

                void ProcessGame_ProcessGameAsset(GameAsset gameAsset)
                {
                    // Version and size first, both metadata. The hash is only worth paying for when
                    // the file actually looks different to what we already had.
                    gameAsset.LoadVersionAndSize();

                    var oldGameAsset = oldGameAssets.FirstOrDefault(x => x.Path.Equals(gameAsset.Path, StringComparison.OrdinalIgnoreCase));

                    if (oldGameAsset is not null && gameAsset.MatchesCachedFile(oldGameAsset))
                    {
                        gameAsset.Hash = oldGameAsset.Hash;
                    }
                    else
                    {
                        gameAsset.LoadHash();
                    }

                    if (oldGameAsset is not null) // DLL existed previously
                    {
                        if (gameAsset.Version == oldGameAsset.Version)
                        {
                            // NOOP
                        }
                        else
                        {
                            dllHistory.Add(new GameHistory()
                            {
                                GameId = ID,
                                EventType = GameHistoryEventType.DLLChangedExternally,
                                EventTime = DateTime.Now,
                                AssetType = gameAsset.AssetType,
                                AssetPath = gameAsset.Path,
                                AssetVersion = gameAsset.DisplayName,
                            });

                            // If the DLL was changed externally (eg. game update) we delete the backup.
                            // This fixes the issue where looking at your game it may appear to be downgraded but
                            // in reality it is because the game updated to a newer version than you had swapped to.
                            var expectedBackupPath = $"{gameAsset.Path}.dlsss";
                            if (File.Exists(expectedBackupPath))
                            {
                                var tempBackupGameAsset = new GameAsset()
                                {
                                    Id = ID,
                                    AssetType = DLLManager.Instance.GetAssetBackupType(gameAsset.AssetType),
                                    Path = expectedBackupPath,
                                };
                                tempBackupGameAsset.LoadVersionAndHash();

                                dllHistory.Add(new GameHistory()
                                {
                                    GameId = ID,
                                    EventType = GameHistoryEventType.DLLBackupRemoved,
                                    EventTime = DateTime.Now,
                                    AssetType = tempBackupGameAsset.AssetType,
                                    AssetPath = tempBackupGameAsset.Path,
                                    AssetVersion = tempBackupGameAsset.DisplayName,
                                });

                                File.Delete(expectedBackupPath);
                            }
                        }
                    }
                    else // DLL is new
                    {
                        dllHistory.Add(new GameHistory()
                        {
                            GameId = ID,
                            EventType = GameHistoryEventType.DLLDetected,
                            EventTime = DateTime.Now,
                            AssetType = gameAsset.AssetType,
                            AssetPath = gameAsset.Path,
                            AssetVersion = gameAsset.DisplayName,
                        });
                    }

                    if (DLLManager.Instance.IsInKnownGameAsset(gameAsset, this) == false)
                    {
                        unknownGameAssets.Add(gameAsset);
                    }

                    if (isFirstTimeSeeingThisGame)
                    {
                        CreateOriginalBackupForGameAsset(gameAsset);
                    }

                    LoadBackupForGameAsset(gameAsset, oldGameAssets);

                }

                foreach (var dllPath in dllPaths)
                {
                    // Matched case insensitively the way Windows treats file names. The chain this
                    // replaced compared exactly, so a game shipping NVNGX_DLSS.DLL went unnoticed.
                    var dllTypeDefinition = DllTypes.ForFileName(Path.GetFileName(dllPath));
                    if (dllTypeDefinition is null)
                    {
                        continue;
                    }

                    var gameAsset = new GameAsset()
                    {
                        Id = ID,
                        AssetType = dllTypeDefinition.AssetType,
                        Path = dllPath,
                    };
                    ProcessGame_ProcessGameAsset(gameAsset);
                    GameAssets.Add(gameAsset);
                }

                App.CurrentApp.RunOnUIThread(() =>
                {
                    UpdateCurrentDLLsFromGameAssets();
                });

                if (GameAssets.Any())
                {
                    newHasSwappableItems = true;

                    //App.CurrentApp.Database.ExecuteAsync
                    //savePoint is not valid, and should be the result of a call to SaveTransactionPoint.
                    using (await Database.Instance.Mutex.LockAsync())
                    {
                        await Database.Instance.Connection.InsertAllAsync(dllHistory, false).ConfigureAwait(false);
                        await Database.Instance.Connection.InsertAllAsync(GameAssets, false).ConfigureAwait(false);
                    }

                    if (unknownGameAssets.Any())
                    {
                        GameManager.Instance.AddUnknownGameAssets(GameLibrary, Title, unknownGameAssets);
                    }
                }

                if (coverImageTask is not null)
                {
                    await coverImageTask;
                }
            }
            catch (Exception err)
            {
                Logger.Error(err);
                Debugger.Break();
            }
            finally
            {
                // Now update all the data on the UI therad.
                await App.CurrentApp.RunOnUIThreadAsync(async () =>
                {
                    HasSwappableItems = newHasSwappableItems;

                    if (autoSave)
                    {
                        await SaveToDatabaseAsync();
                    }

                    Processing = false;
                });
            }
        });
    }

    /// <summary>
    /// Keeps a copy of a dll the first time we ever see the game it belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only safe on first sight. On any later scan we cannot tell a dll the game shipped from one
    /// that was swapped in, so backing up then could record a swapped dll as the original and make
    /// "reset to default" restore the wrong thing.
    /// </para>
    /// <para>
    /// Never overwrites an existing backup, so a game that already has one keeps it.
    /// </para>
    /// </remarks>
    void CreateOriginalBackupForGameAsset(GameAsset gameAsset)
    {
        var backupPath = $"{gameAsset.Path}.dlsss";
        if (File.Exists(backupPath))
        {
            return;
        }

        try
        {
            File.Copy(gameAsset.Path, backupPath);
            Logger.Info($"Backed up {gameAsset.Path} on first detection of {Title}.");
        }
        catch (Exception err)
        {
            // Not worth interrupting a scan for. The game keeps working, it just has no backup,
            // which is where it would have been anyway.
            Logger.Warning($"Could not back up {gameAsset.Path}: {err.Message}");
        }
    }

    /// <summary>
    /// Adds the ".dlsss" backup beside a dll, if there is one.
    /// </summary>
    /// <param name="cachedGameAssets">
    /// What we had for this game before the scan, so an unchanged backup does not need re-hashing.
    /// Backups are the same size as the dlls they shadow, so this is worth as much as it is for
    /// the dlls themselves.
    /// </param>
    void LoadBackupForGameAsset(GameAsset gameAsset, List<GameAsset> cachedGameAssets)
    {
        var backupPath = $"{gameAsset.Path}.dlsss";
        if (File.Exists(backupPath))
        {
            var gameAssetBackup = new GameAsset()
            {
                Id = ID,
                AssetType = DLLManager.Instance.GetAssetBackupType(gameAsset.AssetType),
                Path = backupPath,
            };

            gameAssetBackup.LoadVersionAndSize();

            var cachedBackup = cachedGameAssets.FirstOrDefault(x => x.Path.Equals(backupPath, StringComparison.OrdinalIgnoreCase));
            if (cachedBackup is not null && gameAssetBackup.MatchesCachedFile(cachedBackup))
            {
                gameAssetBackup.Hash = cachedBackup.Hash;
            }
            else
            {
                gameAssetBackup.LoadHash();
            }

            GameAssets.Add(gameAssetBackup);
        }
    }


    public async Task LoadCoverImageAsync()
    {
        if (_isLoadingCoverImage == true)
        {
            return;
        }

        _isLoadingCoverImage = true;

        // TODO: Update if the image last write is > 1 week old or something

        if (File.Exists(ExpectedCustomCoverImage))
        {
            // If a custom cover exists use it.
            App.CurrentApp.RunOnUIThread(() =>
            {
                CoverImage = ExpectedCustomCoverImage;
            });
        }
        else if (File.Exists(ExpectedCoverImage))
        {
            // If a standard cover exists use it.
            App.CurrentApp.RunOnUIThread(() =>
            {
                CoverImage = ExpectedCoverImage;
            });
        }
        else
        {
            // If no cover exists use the abstracted method to get the game as expect for this library.
            await UpdateCacheImageAsync();
        }

        _isLoadingCoverImage = false;
    }

    protected abstract Task UpdateCacheImageAsync();

    internal async Task<(bool Success, string Message, bool PromptToRelaunchAsAdmin)> ResetDllAsync(GameAssetType gameAssetType)
    {
        var backupRecordType = DLLManager.Instance.GetAssetBackupType(gameAssetType);
        var existingBackupRecords = this.GameAssets.Where(x => x.AssetType == backupRecordType).ToList();

        if (existingBackupRecords.Count == 0)
        {
            Logger.Info("No backup records found.");
            return (false, ResourceHelper.GetString("Game_Reset_RepairManually"), false);
        }

        // Pair every backup with the dll it restores before touching anything, so a game missing one
        // of its backups doesn't end up half restored.
        var restorePairs = new List<(GameAsset Backup, GameAsset Current)>();
        foreach (var existingBackupRecord in existingBackupRecords)
        {
            var primaryRecordName = TrimBackupSuffix(existingBackupRecord.Path);
            var existingRecords = this.GameAssets.Where(x => x.AssetType == gameAssetType && x.Path.Equals(primaryRecordName, StringComparison.OrdinalIgnoreCase)).ToList();

            if (existingRecords.Count != 1)
            {
                Logger.Info("Backup record was found, existing records were not.");
                return (false, ResourceHelper.GetString("Game_Reset_RepairManually"), false);
            }

            restorePairs.Add((existingBackupRecord, existingRecords[0]));
        }

        // Locations of this asset type with no backup at all. There is nothing to restore them from,
        // and reporting a plain success while leaving them swapped is how a game ends up running
        // mismatched dlls without the user ever being told.
        var restorableTargets = new HashSet<string>(restorePairs.Select(x => x.Current.Path), StringComparer.OrdinalIgnoreCase);
        var unrestorableRecords = this.GameAssets
            .Where(x => x.AssetType == gameAssetType && restorableTargets.Contains(x.Path) == false)
            .ToList();

        var resetResult = new DllSwapExecutor().Reset(restorePairs.Select(x => x.Current.Path).ToList());

        foreach (var warning in resetResult.Warnings)
        {
            Logger.Warning(warning);
        }

        if (resetResult.Success == false)
        {
            if (resetResult.Error is not null)
            {
                Logger.Error(resetResult.Error);
            }

            if (resetResult.RollbackIncomplete)
            {
                // We couldn't get the game back to how we found it, so our cached view of it can't be trusted.
                NeedsProcessing = true;
            }

            return DescribeResetFailure(resetResult);
        }

        // Only now that the disk is committed do we update our own bookkeeping.
        var dllHistory = new List<GameHistory>();
        var newGameAssets = new List<GameAsset>();

        foreach (var (backupRecord, currentRecord) in restorePairs)
        {
            var newGameAsset = new GameAsset()
            {
                Id = ID,
                AssetType = gameAssetType,
                Path = currentRecord.Path,
                Version = backupRecord.Version,
                Hash = backupRecord.Hash,
            };
            newGameAssets.Add(newGameAsset);

            dllHistory.Add(new GameHistory()
            {
                GameId = ID,
                EventType = GameHistoryEventType.DLLReset,
                EventTime = DateTime.Now,
                AssetType = gameAssetType,
                AssetPath = currentRecord.Path,
                AssetVersion = backupRecord.DisplayName,
            });

            GameAssets.Remove(currentRecord);
            GameAssets.Remove(backupRecord);
        }

        GameAssets.AddRange(newGameAssets);

        foreach (var newGameAsset in newGameAssets)
        {
            UpdateCurrentAsset(newGameAsset, gameAssetType);
        }

        using (await Database.Instance.Mutex.LockAsync())
        {
            await Database.Instance.Connection.InsertAllAsync(dllHistory, false);

            // Update game assets list by deleting and re-adding.
            await Database.Instance.Connection.ExecuteAsync("DELETE FROM game_asset WHERE id = ?", ID).ConfigureAwait(false);
            await Database.Instance.Connection.InsertAllAsync(GameAssets, false).ConfigureAwait(false);
        }

        // Restoring an older dll can put the game behind again, so the badge has to be recomputed.
        RefreshUpdateAvailable();

        if (unrestorableRecords.Count > 0)
        {
            foreach (var unrestorableRecord in unrestorableRecords)
            {
                Logger.Warning($"No backup to restore for {unrestorableRecord.Path}, it has been left unchanged.");
            }

            var totalCount = restorePairs.Count + unrestorableRecords.Count;
            return (true, ResourceHelper.GetFormattedResourceTemplate("Game_Reset_PartialTemplate", restorePairs.Count, totalCount, unrestorableRecords.Count), false);
        }

        return (true, string.Empty, false);
    }

    static string TrimBackupSuffix(string backupPath)
    {
        // Not Replace, that would also mangle a path containing the suffix somewhere in the middle.
        if (backupPath.EndsWith(DllSwapExecutor.BackupSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return backupPath.Substring(0, backupPath.Length - DllSwapExecutor.BackupSuffix.Length);
        }

        return backupPath;
    }

    (bool Success, string Message, bool PromptToRelaunchAsAdmin) DescribeSwapFailure(SwapResult result)
    {
        switch (result.Failure)
        {
            case SwapFailure.SourceMissing:
                return (false, ResourceHelper.GetString("Game_Swap_DownloadedDllNotFound"), false);

            case SwapFailure.NoTargets:
                return (false, ResourceHelper.GetString("Game_Swap_NoDllRecordsToUpdate"), false);

            case SwapFailure.AccessDenied:
                if (App.CurrentApp.IsAdminUser() is false)
                {
                    return (false, ResourceHelper.GetString("Game_Swap_AccessDeniedAdmin"), true);
                }
                return (false, ResourceHelper.GetString("Game_Swap_AccessDenied"), false);

            case SwapFailure.FileInUse:
                return (false, ResourceHelper.GetString("Game_Swap_FileInUse"), false);

            default:
                return (false, ResourceHelper.GetString("Game_Swap_UnknownError"), false);
        }
    }

    (bool Success, string Message, bool PromptToRelaunchAsAdmin) DescribeResetFailure(SwapResult result)
    {
        switch (result.Failure)
        {
            case SwapFailure.AccessDenied:
                if (App.CurrentApp.IsAdminUser() is false)
                {
                    return (false, ResourceHelper.GetString("Game_Reset_AccessDeniedAdmin"), true);
                }
                return (false, ResourceHelper.GetString("Game_Reset_RepairManually"), false);

            case SwapFailure.FileInUse:
                return (false, ResourceHelper.GetString("Game_Reset_FileInUse"), false);

            default:
                return (false, ResourceHelper.GetString("Game_Reset_RepairManually"), false);
        }
    }

    /// <summary>
    /// Attempts to update a DLSS dll in a given game.
    /// </summary>
    /// <param name="dlssRecord"></param>
    /// <returns>Tuple containing a boolean of Success, if this is false there will be an error message in the Message response.</returns>
    internal async Task<(bool Success, string Message, bool PromptToRelaunchAsAdmin)> UpdateDllAsync(DLLRecord dllRecord)
    {
        if (dllRecord is null)
        {
            return (false, ResourceHelper.GetString("Game_Swap_DllRecordNotFound"), false);
        }

        if (dllRecord.LocalRecord is null)
        {
            return (false, ResourceHelper.GetString("Game_Swap_LocalDllRecordNotFound"), false);
        }

        if (File.Exists(dllRecord.LocalRecord.ExpectedPath) == false)
        {
            return (false, ResourceHelper.GetString("Game_Swap_DownloadedDllNotFound"), false);
        }

        var existingRecords = this.GameAssets.Where(x => x.AssetType == dllRecord.AssetType).ToList();
        if (existingRecords.Count == 0)
        {
            return (false, ResourceHelper.GetString("Game_Swap_NoDllRecordsToUpdate"), false);
        }

        var backupRecordType = DLLManager.Instance.GetAssetBackupType(dllRecord.AssetType);

        var versionInfo = FileVersionInfo.GetVersionInfo(dllRecord.LocalRecord.ExpectedPath);
        var dllVersion = versionInfo.GetFormattedFileVersion();
        var md5Hash = versionInfo.GetMD5Hash();
        if (dllRecord.MD5Hash != md5Hash)
        {
            return (false, ResourceHelper.GetString("Game_Swap_InvalidHash"), false);
        }


        // Validate new DLL
        if (Settings.Instance.AllowUntrusted == false)
        {
            var isTrusted = WinTrust.VerifyEmbeddedSignature(dllRecord.LocalRecord.ExpectedPath);
            if (isTrusted == false)
            {
                return (false, ResourceHelper.GetString("Game_Swap_UntrustedSignature"), false);
            }
        }

        // Every location this game keeps the dll in is swapped as one operation. The executor backs up
        // each one that needs it, stages the writes, and puts everything back if any step fails, so a
        // failure here means nothing on disk changed.
        var swapResult = new DllSwapExecutor().Swap(dllRecord.LocalRecord.ExpectedPath, existingRecords.Select(x => x.Path).ToList());

        foreach (var warning in swapResult.Warnings)
        {
            Logger.Warning(warning);
        }

        if (swapResult.Success == false)
        {
            if (swapResult.Error is not null)
            {
                Logger.Error(swapResult.Error);
            }

            if (swapResult.RollbackIncomplete)
            {
                // We couldn't get the game back to how we found it, so our cached view of it can't be trusted.
                NeedsProcessing = true;
            }

            return DescribeSwapFailure(swapResult);
        }

        // Only now that the disk is committed do we update our own bookkeeping.
        var newGameAssets = new List<GameAsset>();

        foreach (var createdBackup in swapResult.CreatedBackups)
        {
            var backedUpRecord = existingRecords.First(x => x.Path.Equals(createdBackup.TargetPath, StringComparison.OrdinalIgnoreCase));

            newGameAssets.Add(new GameAsset()
            {
                Id = ID,
                AssetType = backupRecordType,
                Path = createdBackup.BackupPath,
                Version = backedUpRecord.Version,
                Hash = backedUpRecord.Hash,
            });
        }

        var dllHistory = new List<GameHistory>();

        foreach (var existingRecord in existingRecords)
        {
            // No need to call LoadVersionAndHash, the data is already here.
            newGameAssets.Add(new GameAsset()
            {
                Id = ID,
                AssetType = dllRecord.AssetType,
                Path = existingRecord.Path,
                Version = dllVersion,
                Hash = dllRecord.MD5Hash,
            });

            dllHistory.Add(new GameHistory()
            {
                GameId = ID,
                EventType = GameHistoryEventType.DLLSwapped,
                EventTime = DateTime.Now,
                AssetType = dllRecord.AssetType,
                AssetPath = existingRecord.Path,
                AssetVersion = dllRecord.DisplayName,
            });
        }

        foreach (var existingRecrod in existingRecords)
        {
            GameAssets.Remove(existingRecrod);
        }
        GameAssets.AddRange(newGameAssets);

        // This should never be null.
        // Using FirstOrDefault as there may be multiple, but we only care about using the information of the first.
        var firstNewGameAsset = newGameAssets.FirstOrDefault(x => x.AssetType == dllRecord.AssetType);
        if (firstNewGameAsset is not null)
        {
            UpdateCurrentAsset(firstNewGameAsset, dllRecord.AssetType);
        }

        // Update game assets list by deleting and re-adding.
        using (await Database.Instance.Mutex.LockAsync())
        {
            await Database.Instance.Connection.InsertAllAsync(dllHistory, false);
            await Database.Instance.Connection.ExecuteAsync("DELETE FROM game_asset WHERE id = ?", ID).ConfigureAwait(false);
            await Database.Instance.Connection.InsertAllAsync(GameAssets, false).ConfigureAwait(false);
        }

        // The game is no longer behind on this dll, so the badge has to be recomputed. Without this
        // it only refreshed when the manifest reloaded or the game was rescanned.
        RefreshUpdateAvailable();

        return (true, string.Empty, false);
    }

    void UpdateCurrentAsset(GameAsset newGameAsset, GameAssetType gameAssetType)
    {
        App.CurrentApp.RunOnUIThread(() =>
        {
            var assetSlot = GetAssetSlot(gameAssetType);
            if (assetSlot is null)
            {
                Logger.Error($"Unknown AssetType: {gameAssetType}");
                return;
            }

            // Cleared first so the change is raised even when the same instance comes back, which
            // is what the assignments this replaced were doing.
            assetSlot.CurrentAsset = null;
            assetSlot.CurrentAsset = newGameAsset;
        });
    }

    #region IComparable<Game>
    public int CompareTo(Game? other)
    {
        if (other is null)
        {
            return -1;
        }

        return Title.CompareTo(other.Title);
    }
    #endregion

    /*
    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged = null;
    void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion
    */


    protected async Task ResizeCoverAsync(Stream imageStream)
    {
        // TODO:
        // - find optimal format (eg, is displaying 100 webp images more intense than 100 png images)
        // - load image based on scale
        try
        {
            using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imageStream).ConfigureAwait(false))
            {
                // If images are really big we resize to at least 2x the 200x300 we display as.
                // In future this should be updated to resize to display scale.
                // If the image is smaller than this we are just saving as png.
                var resizeOptions = new ResizeOptions()
                {
                    Size = new Size(200 * 2, 300 * 2),
                    Sampler = KnownResamplers.Lanczos5,
                    Mode = ResizeMode.Min, // If image is smaller it won't be resized up.
                };
                image.Mutate(x => x.Resize(resizeOptions));
                image.SaveAsPng(ExpectedCoverImage);
                //image.SaveAsWebp(ExpectedCoverImage);
                //image.SaveAsJpeg(ExpectedCoverImage);
            }

            App.CurrentApp.RunOnUIThread(() =>
            {
                CoverImage = null;
                CoverImage = ExpectedCoverImage;
            });
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }


    public void AddCustomCover(string imageSource)
    {
        using (var fileStream = File.OpenRead(imageSource))
        {
            AddCustomCover(fileStream);
        }
    }

    public void AddCustomCover(Stream stream)
    {
        // TODO:
        // - find optimal format (eg, is displaying 100 webp images more intense than 100 png images)
        // - load image based on scale
        try
        {
            using (var image = SixLabors.ImageSharp.Image.Load(stream))
            {
                // If images are really big we resize to at least 3x the 200x300 we display as.
                // In future this should be updated to resize to display scale.
                // If the image is smaller than this we are just saving as png.
                var resizeOptions = new ResizeOptions()
                {
                    Size = new Size(200 * 3, 300 * 3),
                    Sampler = KnownResamplers.Lanczos5,
                    Mode = ResizeMode.Min, // If image is smaller it won't be resized up.
                };
                image.Mutate(x => x.Resize(resizeOptions));
                image.SaveAsPng(ExpectedCustomCoverImage);
                //image.SaveAsWebp(ExpectedCustomCoverImage);
                //image.SaveAsJpeg(ExpectedCustomCoverImage);
            }

            App.CurrentApp.RunOnUIThread(() =>
            {
                CoverImage = ExpectedCustomCoverImage;
            });
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }

    protected async Task<bool> DownloadCoverAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Logger.Error($"Tried to download cover image but url was null or empty. Game: {Title}, Library: {GameLibrary}");
            return false;
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == false &&
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == false)
        {
            Logger.Error($"Tried to download cover image but url was not valid. Game: {Title}, Library: {GameLibrary}, Url: {url}");
            return false;
        }


        var extension = Path.GetExtension(url);

        // Path.GetExtension retains query arguments, so ths will remove them if they exist.
        if (extension.Contains('?'))
        {
            extension = extension.Substring(0, extension.IndexOf("?"));
        }
        var tempFile = Path.Combine(Storage.GetTemp(), $"{ID}{extension}");


        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var fileDownloader = new FileDownloader(url, 0);
                await fileDownloader.DownloadFileToStreamAsync(memoryStream).ConfigureAwait(false);
                memoryStream.Position = 0;

                // Now if the image is downloaded lets resize it,
                await ResizeCoverAsync(memoryStream).ConfigureAwait(false);
            }
            return true;
        }
        catch (Exception err)
        {
            Logger.Error(err, $"For url: {url}");
            //Debugger.Break();
            return false;
        }
        finally
        {
            // Cleanup temp file.
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    public async Task SaveToDatabaseAsync()
    {
        try
        {
            var rowsChanged = -1;
            using (await Database.Instance.Mutex.LockAsync())
            {
                rowsChanged = await Database.Instance.Connection.InsertOrReplaceAsync(this);
                // tODO: Configure await
            }
            if (rowsChanged == 0)
            {
                // TODO: Fix why this happens occasionally to reandom games.
                // This appears to change to different games in different libraries.
                Logger.Error($"Tried to save game to database but rowsChanged was 0.");
                //Debugger.Break();
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
            Debugger.Break();
        }
    }

    public async Task DeleteAsync()
    {
        try
        {
            // Sometimes when a game is uninstalled the backup files are not removed, so ensure they are.
            // https://github.com/beeradmoore/dlss-swapper/issues/236

            List<GameAsset> gameAssets;
            using (await Database.Instance.Mutex.LockAsync())
            {
                gameAssets = await Database.Instance.Connection.Table<GameAsset>().Where(ga => ga.Id == ID).ToListAsync();
            }
            foreach (var cachedGameAsset in gameAssets)
            {
                // If its a file we made we should attempt to delete it.
                if (DllTypes.IsBackupAssetType(cachedGameAsset.AssetType))
                {
                    if (File.Exists(cachedGameAsset.Path))
                    {
                        Logger.Info($"Deleting {cachedGameAsset.Path}");
                        try
                        {
                            File.Delete(cachedGameAsset.Path);
                        }
                        catch (Exception err)
                        {
                            Logger.Error(err, $"Could not delete {cachedGameAsset.Path}");
                        }
                    }
                }
            }
            using (await Database.Instance.Mutex.LockAsync())
            {
                await Database.Instance.Connection.Table<GameAsset>().DeleteAsync(ga => ga.Id == ID).ConfigureAwait(false);
            }

            // Delete the thumbnails.
            var thumbnailImages = Directory.GetFiles(Storage.GetImageCachePath(), $"{ID}_*", SearchOption.AllDirectories);
            foreach (var thumbnailImage in thumbnailImages)
            {
                try
                {
                    Logger.Info($"Deleting {thumbnailImage}");
                    File.Delete(thumbnailImage);
                }
                catch (Exception err)
                {
                    Logger.Error(err, $"Could not delete {thumbnailImage}");
                }
            }

            // Delete the game itself.
            using (await Database.Instance.Mutex.LockAsync())
            {
                await Database.Instance.Connection.DeleteAsync(this).ConfigureAwait(false);
            }

            // Remove the game from the list.
            GameManager.Instance.RemoveGame(this);
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }

    public async Task PromptToRemoveCustomCover()
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
            CoverImage = null;

            if (File.Exists(ExpectedCustomCoverImage))
            {
                File.Delete(ExpectedCustomCoverImage);
            }

            if (this.GameLibrary == GameLibrary.ManuallyAdded)
            {
                await SaveToDatabaseAsync();
            }

            // Will load default or attempt to fetch fresh.
            await LoadCoverImageAsync();
        }
    }

    public void PromptToBrowseCustomCover()
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
                return;
            }

            AddCustomCover(coverImageFile);
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }

    public bool Equals(Game? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ID == other.ID)
        {
            return true;
        }

        if (PlatformId == other.PlatformId)
        {
            return true;
        }

        return false;
    }

    protected bool ParentUpdateFromGame(Game game)
    {
        var didChange = false;

        if (Title != game.Title)
        {
            Title = game.Title;
            didChange = true;
        }

        if (InstallPath != game.InstallPath)
        {
            InstallPath = PathHelpers.NormalizePath(game.InstallPath);
            didChange = true;
        }

        if (CoverImage != game.CoverImage)
        {
            CoverImage = game.CoverImage;
            didChange = true;
        }

        if (HasSwappableItems != game.HasSwappableItems)
        {
            HasSwappableItems = game.HasSwappableItems;
            didChange = true;
        }

        // Compare each installed dll through its slot instead of a chain of named properties.
        // Reference comparison, matching what the properties this replaced did.
        foreach (var assetSlot in _assetSlots)
        {
            var otherAssetSlot = game.GetAssetSlot(assetSlot.AssetType);
            if (assetSlot.CurrentAsset != otherAssetSlot?.CurrentAsset)
            {
                assetSlot.CurrentAsset = otherAssetSlot?.CurrentAsset;
                didChange = true;
            }
        }


        if (DlssPreset != game.DlssPreset)
        {
            DlssPreset = game.DlssPreset;
            didChange = true;
        }

        if (DlssDPreset != game.DlssDPreset)
        {
            DlssDPreset = game.DlssDPreset;
            didChange = true;
        }

        // We don't copy across the following properties as it is assume this object has the latest revisions:
        // - Notes
        // - IsFavourite

        return didChange;
    }

    public abstract bool UpdateFromGame(Game game);

    void UpdateCurrentDLLsFromGameAssets()
    {
        foreach (var assetSlot in _assetSlots)
        {
            var assetsForType = GameAssets.Where(x => x.AssetType == assetSlot.AssetType).ToList();

            assetSlot.MultipleFound = assetsForType.Count > 1;

            // Last one wins, matching the chain of assignments this replaced.
            assetSlot.CurrentAsset = assetsForType.LastOrDefault();
        }

        RefreshUpdateAvailable();
    }

    /// <summary>
    /// The asset types a game can have swapped.
    /// </summary>
    static IEnumerable<GameAssetType> EnumerateSwappableAssetTypes()
    {
        return DllTypes.All.Select(x => x.AssetType);
    }

    /// <summary>
    /// Works out whether any installed dll has a newer version available to swap to.
    /// </summary>
    /// <remarks>
    /// Called whenever the installed dlls change, and again once the manifest loads, because on a
    /// cold start the games are read from cache before we know what versions exist.
    /// </remarks>
    public void RefreshUpdateAvailable()
    {
        // Ranked here, decided in the core library where the rules are tested.
        var latestRankByAssetType = new Dictionary<GameAssetType, ulong>();
        foreach (var assetType in EnumerateSwappableAssetTypes())
        {
            var latestRecord = DLLManager.Instance.GetLatestRecord(assetType);
            if (latestRecord is null)
            {
                continue;
            }

            if (DllVersionRanking.TryGetRank(assetType, latestRecord.InternalName, latestRecord.Version, out var latestRank))
            {
                latestRankByAssetType[assetType] = latestRank;
            }
        }

        var installedDlls = new List<InstalledDll>();
        foreach (var gameAsset in GameAssets)
        {
            // A dll whose version we cannot read is left out rather than guessed at.
            if (TryGetInstalledVersionNumber(gameAsset, gameAsset.AssetType, out var installedRank))
            {
                installedDlls.Add(new InstalledDll(gameAsset.AssetType, installedRank));
            }
        }

        var outdatedAssetTypes = UpdateAvailability.FindOutdatedTypes(installedDlls, latestRankByAssetType);

        // One badge per vendor rather than per dll, otherwise a game trailing on four Intel dlls
        // would show four identical dots. The tooltip names the specific dlls instead.
        var availableUpdates = outdatedAssetTypes
            .GroupBy(x => DLLManager.Instance.GetAssetVendor(x))
            .Where(x => x.Key != DllVendor.Unknown)
            .OrderBy(x => x.Key)
            .Select(x => new DllVendorUpdate()
            {
                Vendor = x.Key,
                Label = DLLManager.Instance.GetVendorShortName(x.Key),
                ToolTip = ResourceHelper.GetFormattedResourceTemplate("GameGrid_UpdateAvailableTemplate", string.Join(", ", x.Select(y => DLLManager.Instance.GetAssetTypeName(y)))),
            })
            .ToList();

        App.CurrentApp.RunOnUIThread(() =>
        {
            OutdatedAssetTypes = outdatedAssetTypes;
            AvailableUpdates = availableUpdates;
            UpdateAvailable = availableUpdates.Count > 0;
        });
    }

    /// <summary>
    /// Resolves the installed dll to the manifest's packed version number.
    /// </summary>
    /// <remarks>
    /// Matching on hash is exact, but a dll the game shipped with is often not in the manifest at
    /// all, so we fall back to the version recorded off the file itself.
    /// </remarks>
    static bool TryGetInstalledVersionNumber(GameAsset gameAsset, GameAssetType assetType, out ulong versionNumber)
    {
        if (string.IsNullOrWhiteSpace(gameAsset.Hash) == false)
        {
            var knownRecord = DLLManager.Instance.GetRecords(assetType)?.FirstOrDefault(x => x.MD5Hash == gameAsset.Hash);
            if (knownRecord is not null)
            {
                return DllVersionRanking.TryGetRank(assetType, knownRecord.InternalName, knownRecord.Version, out versionNumber);
            }
        }

        // Not in the manifest, so the game shipped it. DisplayVersion resolves the sdk version for
        // the types that are ranked by it, and is ignored for the rest.
        return DllVersionRanking.TryGetRank(assetType, gameAsset.DisplayVersion, gameAsset.Version, out versionNumber);
    }

    public async Task RemoveGameAssetsFromCacheAsync()
    {
        using (await Database.Instance.Mutex.LockAsync())
        {
            await Database.Instance.Connection.ExecuteAsync("DELETE FROM game_asset WHERE id = ?", ID).ConfigureAwait(false);
        }
    }

    public async Task LoadGameAssetsFromCacheAsync()
    {
        await LoadCoverImageAsync();

        GameAssets.Clear();
        using (await Database.Instance.Mutex.LockAsync())
        {
            var gameAssets = await Database.Instance.Connection.Table<GameAsset>().Where(ga => ga.Id == ID).ToListAsync().ConfigureAwait(false);
            if (gameAssets?.Any() == true)
            {
                GameAssets.AddRange(gameAssets);
            }
        }

        UpdateCurrentDLLsFromGameAssets();

        // TODO: Add auto reload by storing last full reload time on game

        if (GameAssets.Any())
        {
            foreach (var gameAsset in GameAssets)
            {
                // Check that each of the game assets exist, after we will check if they are what we expect them to be
                if (File.Exists(gameAsset.Path) == false)
                {
                    NeedsProcessing = true;
                    break;
                }
            }

            if (NeedsProcessing == false)
            {
                var unknownGameAssets = new List<GameAsset>();
                foreach (var gameAsset in GameAssets)
                {
                    if (DLLManager.Instance.IsInKnownGameAsset(gameAsset, this) == false)
                    {
                        unknownGameAssets.Add(gameAsset);
                    }
                }
                if (unknownGameAssets.Any())
                {
                    GameManager.Instance.AddUnknownGameAssets(GameLibrary, Title, unknownGameAssets);
                }

                foreach (var gameAsset in GameAssets)
                {
                    var fileVersionInfo = FileVersionInfo.GetVersionInfo(gameAsset.Path);
                    var freshVersion = fileVersionInfo.GetFormattedFileVersion();

                    if (gameAsset.Version != freshVersion)
                    {
                        NeedsProcessing = true;
                        break;
                    }
                }
            }
        }
        else
        {
            // If there is no known current DLLs then we likely want to do a full reload in case the game got updated.
            // TODO: Also add a time last reloaded here.
            NeedsProcessing = true;
            return;
        }
    }

    public bool IsInIgnoredPath()
    {
        // If there are no ignored paths we can skip this altogether.
        if (Settings.Instance.IgnoredPaths.Length == 0)
        {
            return false;
        }

        // If installed path is empty we should consider it ignored.
        if (string.IsNullOrWhiteSpace(InstallPath))
        {
            return true;
        }

        foreach (var ignoredPath in Settings.Instance.IgnoredPaths)
        {
            // Because we make IgnoredPaths have a / on the end it will fail the below check.
            // In the cases where the path could be off by one we will do a manual check.
            if (ignoredPath.Length - 1 == InstallPath.Length)
            {
                var tempInstallPath = InstallPath + Path.DirectorySeparatorChar;
                if (tempInstallPath.Equals(ignoredPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }


            if (InstallPath.StartsWith(ignoredPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
