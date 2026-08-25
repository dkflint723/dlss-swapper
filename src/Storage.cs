using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DLSS_Swapper;

// TODO: Test portable app.
// TODO: Clean portable temp path on launch

/*
 * For notes on where data is stored please see https://github.com/beeradmoore/dlss-swapper/wiki/Local-Data-Structure 
 */
static class Storage
{
    static string? _storagePath;
#if   PORTABLE && DEBUG
    //public static string StoragePath => _storagePath ??= Path.Combine(AppContext.BaseDirectory, "StoredData", "DEBUG", Guid.NewGuid().ToString());
    public static string StoragePath => _storagePath ??= Path.Combine(AppContext.BaseDirectory, "StoredData", "DEBUG");
#elif PORTABLE && !DEBUG
    public static string StoragePath => _storagePath ??= Path.Combine(AppContext.BaseDirectory, "StoredData");
#elif !PORTABLE && DEBUG
    //public static string StoragePath => _storagePath ??= Path.Combine(Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"), "DLSS Swapper", "DEBUG", Guid.NewGuid().ToString());
    public static string StoragePath => _storagePath ??= Path.Combine(Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"), "DLSS Swapper", "DEBUG");
#elif !PORTABLE && !DEBUG
    public static string StoragePath => _storagePath  ??= Path.Combine(Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"), "DLSS Swapper");
#endif

    /// <summary>
    /// Points storage at a different folder.
    /// </summary>
    /// <remarks>
    /// For tests, which need a real database to check that changes survive a reload but must not be
    /// able to touch the one the user's own install is using. A debug build resolves the path above
    /// to the same folder the developer's copy of the app uses, so without this a test run would
    /// rewrite a real library.
    /// </remarks>
    internal static void OverrideStoragePath(string path)
    {
        _storagePath = path;

        // The static constructor made these under the previous path and has already run, so pointing
        // somewhere new leaves the new folder without them. Anything writing settings or a manifest
        // then fails on a missing directory rather than on whatever it was actually testing.
        CreateDirectoryIfNotExists(GetStorageFolder());
        CreateDirectoryIfNotExists(GetDynamicJsonFolder());
        CreateDirectoryIfNotExists(GetImageCachePath());
    }


    static Storage()
    {
        // Create directories if they doesn't exist.
        //CreateDirectoryIfNotExists(GetTemp());
        CreateDirectoryIfNotExists(GetStorageFolder());
        CreateDirectoryIfNotExists(GetDynamicJsonFolder());
        CreateDirectoryIfNotExists(GetImageCachePath());
    }

    public static string GetTemp()
    {
#if PORTABLE
        var path = Path.Combine(StoragePath, "temp");
        CreateDirectoryIfNotExists(path);
#else
        var path = Path.Combine(Path.GetTempPath(), "DLSS Swapper");
        CreateDirectoryIfNotExists(path);
#endif
        return path;
    }

    public static string GetStorageFolder()
    {
        return StoragePath;
    }

    public static string GetDynamicJsonFolder()
    {
        return Path.Combine(StoragePath, "json");
    }

    public static string GetUpdatesFolder()
    {
        return Path.Combine(GetTemp(), "updates");
    }

    public static string GetDBPath()
    {
        CreateDirectoryIfNotExists(StoragePath);
        return Path.Combine(StoragePath, "dlss_swapper.db");
    }

    public static string GetImageCachePath()
    {
        return Path.Combine(StoragePath, "image_cache");
    }

    public static string GetReleasesPath()
    {
        return Path.Combine(GetDynamicJsonFolder(), "releases.json");
    }

    public static string GetManifestPath()
    {
        return Path.Combine(GetDynamicJsonFolder(), "manifest.json");
    }

    public static string GetImportedManifestPath()
    {
        return Path.Combine(GetDynamicJsonFolder(), "imported_manifest.json");
    }

    /// <summary>
    /// When given a file path it will make the directory structure so that file is ready to be created in. A directory should not be passed to this. Use CreateDirectoryIfNotExists instead for that.
    /// </summary>
    /// <param name="path">File path</param>
    /// <returns>True if the directory could be created</returns>
    public static bool CreateDirectoryForFileIfNotExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Logger.Error("A path should not be empty in CreateDirectoryForFileIfNotExists");
            return false;
        }

        if (Directory.Exists(path))
        {
            Logger.Error("A directory should not be passed to CreateDirectoryForFileIfNotExists");
            return false;
        }
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            Logger.Error("A directory should not be empty in CreateDirectoryForFileIfNotExists");
            return false;
        }
        return CreateDirectoryIfNotExists(directory);
    }

    /// <summary>
    /// Creates a directory if it doesn't already exist.
    /// </summary>
    /// <param name="directory">Directory to be created</param>
    /// <returns>True if the directory could be created</returns>
    public static bool CreateDirectoryIfNotExists(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            if (Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }
            return true;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            return false;
        }
    }

    /// <summary>
    /// Saves the current settings object to settings.json in the apps dynamic json folder.
    /// </summary>
    /// <param name="settings">Settings object to be saved</param>
    /// <returns>Task</returns>
    internal static void SaveSettingsJson(Settings settings)
    {
        var settingsFile = Path.Combine(GetDynamicJsonFolder(), "settings.json");

        WriteFileAtomically(settingsFile, stream =>
        {
            JsonSerializer.Serialize(stream, settings, SourceGenerationContext.Default.Settings);
        });
    }

    /// <summary>
    /// Writes a file by building it beside the target and moving it into place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three json files this app owns - settings, the imported dll manifest, and the cached
    /// GitHub release - were each opened with <c>FileMode.Create</c>, which truncates the existing
    /// file before a single byte of the replacement is written. Anything that interrupts the write
    /// leaves an empty or half written file where the real one was. Settings are rewritten on almost
    /// every toggle, so the window for that is not small.
    /// </para>
    /// <para>
    /// Building beside it and moving over means the target is either entirely the old file or
    /// entirely the new one. <c>File.Move</c> with overwrite is a rename within one folder, which is
    /// the same shape <see cref="DLSS_Swapper.Swapping.DllSwapExecutor"/> already uses to replace a
    /// dll inside a game, and for the same reason.
    /// </para>
    /// <para>
    /// The temporary file is removed when the write fails, so a failure leaves nothing behind and,
    /// importantly, leaves the existing file untouched.
    /// </para>
    /// </remarks>
    /// <returns>Whether the file was written.</returns>
    internal static bool WriteFileAtomically(string path, Action<Stream> write)
    {
        var temporaryPath = path + ".tmp";

        try
        {
            using (var stream = File.Open(temporaryPath, FileMode.Create))
            {
                write(stream);
            }

            File.Move(temporaryPath, path, true);

            return true;
        }
        catch (Exception err)
        {
            Logger.Error(err);

            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanupErr)
            {
                // A leftover .tmp is harmless - the next write overwrites it - and is not worth
                // failing a save that has already failed.
                Logger.Error(cleanupErr);
            }

            return false;
        }
    }

    /// <summary>The same, for callers that already have the bytes rather than an object.</summary>
    internal static async Task<bool> WriteFileAtomicallyAsync(string path, Func<Stream, Task> writeAsync)
    {
        var temporaryPath = path + ".tmp";

        try
        {
            using (var stream = File.Open(temporaryPath, FileMode.Create))
            {
                await writeAsync(stream).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, true);

            return true;
        }
        catch (Exception err)
        {
            Logger.Error(err);

            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanupErr)
            {
                Logger.Error(cleanupErr);
            }

            return false;
        }
    }

    /// <summary>
    /// Loads settings from settings.json in the apps dynamic json folder.
    /// </summary>
    /// <returns>Settings object, or null if it could not be loaded</returns>
    /// <summary>How a settings load turned out.</summary>
    /// <remarks>
    /// Three answers rather than one, because the caller does very different things with them and
    /// used to be told only "null". A file that is merely absent means a first run, and writing
    /// defaults over it is right. A file that exists and could not be read is somebody's settings,
    /// and writing defaults over it destroyed them - which happened not only to a corrupted file but
    /// to a file an antivirus or backup agent had open for a moment.
    /// </remarks>
    internal enum SettingsLoadOutcome
    {
        /// <summary>Read and parsed.</summary>
        Loaded,

        /// <summary>No file yet. A first run.</summary>
        Missing,

        /// <summary>There is a file and it is not valid json.</summary>
        Corrupt,

        /// <summary>There is a file and it could not be opened. Very possibly temporary.</summary>
        Unreadable,
    }

    /// <summary>
    /// Loads settings from settings.json in the apps dynamic json folder.
    /// </summary>
    internal static (SettingsLoadOutcome Outcome, Settings? Settings) LoadSettingsJson()
    {
        var settingsFile = Path.Combine(GetDynamicJsonFolder(), "settings.json");

        if (File.Exists(settingsFile) == false)
        {
            return (SettingsLoadOutcome.Missing, null);
        }

        try
        {
            using (var stream = File.OpenRead(settingsFile))
            {
                var settings = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.Settings);

                // Valid json that is not a settings object - "null" on its own reads this way.
                return settings is null
                    ? (SettingsLoadOutcome.Corrupt, null)
                    : (SettingsLoadOutcome.Loaded, settings);
            }
        }
        catch (JsonException err)
        {
            // The file is there and is not settings. Reading it again later will not help.
            Logger.Error(err, $"{settingsFile} is not readable as settings.");

            return (SettingsLoadOutcome.Corrupt, null);
        }
        catch (Exception err)
        {
            // Locked, denied, a disconnected drive. The file may be perfectly good, so nothing may
            // overwrite it on the strength of this.
            Logger.Error(err, $"Could not open {settingsFile}.");

            return (SettingsLoadOutcome.Unreadable, null);
        }
    }

    /// <summary>
    /// Moves an unreadable settings file aside so a fresh one can be written without destroying it.
    /// </summary>
    /// <remarks>
    /// Only for a file that parsed as invalid json, never for one that merely would not open. The
    /// user has lost their settings either way at that point; keeping the bytes costs nothing and
    /// is the difference between "gone" and "gone but here it is".
    /// </remarks>
    internal static void MoveUnreadableSettingsAside()
    {
        var settingsFile = Path.Combine(GetDynamicJsonFolder(), "settings.json");

        try
        {
            if (File.Exists(settingsFile))
            {
                File.Move(settingsFile, settingsFile + ".unreadable", true);
                Logger.Warning($"Kept the unreadable settings as {settingsFile}.unreadable and started fresh.");
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }
}
