using System;
using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// A real database in a throwaway folder, plus somewhere to put throwaway dll files.
/// </summary>
/// <remarks>
/// <para>
/// The bugs this exists for all look the same: something is changed in memory and the durable copy
/// is not told. They look correct for as long as the app keeps running and only appear after a
/// restart, which is the moment nobody is watching. No amount of asserting on objects catches that,
/// because the object is right; it is the row that is missing.
/// </para>
/// <para>
/// It uses the same sqlite-net the app ships, against a file in the temp folder. Storage is pointed
/// at that folder first, because a debug build otherwise resolves to the same place the developer's
/// own copy of the app keeps its library.
/// </para>
/// </remarks>
internal sealed class TemporaryDatabase : IAsyncDisposable
{
    internal string Root { get; }

    /// <summary>A folder to create fake game files in, inside the same throwaway root.</summary>
    internal string GameFolder { get; }

    TemporaryDatabase(string root)
    {
        Root = root;
        GameFolder = Path.Combine(root, "game");
        Directory.CreateDirectory(GameFolder);
    }

    internal static async Task<TemporaryDatabase> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "dlss-swapper-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        Storage.OverrideStoragePath(root);

        // Any connection opened against a previous folder has to go, or the singleton keeps using it.
        await Database.ResetInstanceAsync();

        // Constructing it creates the file and runs the table creation and migrations, which is
        // itself worth exercising: a new column that fails to migrate shows up here.
        Database.Instance.Init();

        return new TemporaryDatabase(root);
    }

    /// <summary>Writes a fake dll of a given size, so backups have something real to copy.</summary>
    internal string WriteFakeDll(string fileName, int bytes = 2048)
    {
        var path = Path.Combine(GameFolder, fileName);
        var contents = new byte[bytes];
        for (var index = 0; index < contents.Length; index += 1)
        {
            contents[index] = (byte)(index % 251);
        }

        File.WriteAllBytes(path, contents);
        return path;
    }

    public async ValueTask DisposeAsync()
    {
        await Database.ResetInstanceAsync();

        try
        {
            Directory.Delete(Root, true);
        }
        catch (IOException)
        {
            // A file still held open would leave a folder behind in temp. Not worth failing a test
            // that has already made its assertions.
        }
    }
}

/// <summary>
/// Tests that use a real database share one process wide singleton, so they must not run at the
/// same time as each other or as anything else touching it.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class DatabaseCollection
{
    public const string Name = "database";
}
