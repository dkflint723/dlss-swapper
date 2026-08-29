using System;
using System.IO;
using DLSS_Swapper;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Moving the data folder out of the app's old name.
/// </summary>
/// <remarks>
/// <para>
/// The app was called DLSS Swapper and kept everything under a folder of that name. What is in
/// there is not replaceable: the database carries pins, notes and history, and beside it are the
/// copies of the dlls each game shipped with - the files that exist so that restore has something
/// to put back, and that nothing can recreate. A rename that pointed at a new folder and left that
/// behind would read as an empty library and, to anyone who then pressed restore, as data loss.
/// </para>
/// <para>
/// So the rule under test is narrow and worth stating: move it when there is something to move,
/// never merge, and when the move cannot be done keep using the folder the data is actually in.
/// </para>
/// </remarks>
public class StorageMigrationTests : IDisposable
{
    readonly string _root;

    public StorageMigrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "swapshelf-migration-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
        catch (Exception)
        {
            // A temp folder that outlives the run is not worth failing a test over.
        }
    }

    string Previous => Path.Combine(_root, Storage.PreviousFolderName);
    string Current => Path.Combine(_root, Storage.CurrentFolderName);

    static void WriteMarker(string folder, string name, string content)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name), content);
    }

    [Fact]
    public void AnOldFolderIsMovedToTheNewName()
    {
        WriteMarker(Previous, "dlss_swapper.db", "the library");
        WriteMarker(Path.Combine(Previous, "dlls"), "nvngx_dlss.dll.dlsss", "a saved original");

        var resolved = Storage.ResolveRootFolder(_root);

        Assert.Equal(Current, resolved);
        Assert.False(Directory.Exists(Previous));

        // The point of the whole exercise: what was in there is still in there.
        Assert.Equal("the library", File.ReadAllText(Path.Combine(Current, "dlss_swapper.db")));
        Assert.Equal("a saved original", File.ReadAllText(Path.Combine(Current, "dlls", "nvngx_dlss.dll.dlsss")));
    }

    [Fact]
    public void AFreshInstallJustUsesTheNewName()
    {
        var resolved = Storage.ResolveRootFolder(_root);

        Assert.Equal(Current, resolved);

        // Nothing is created here; that is the static constructor's job afterwards.
        Assert.False(Directory.Exists(Previous));
    }

    /// <summary>
    /// Once the new folder exists, an old one beside it is left alone.
    /// </summary>
    /// <remarks>
    /// Never merged. Two libraries that both think they are current cannot be reconciled by moving
    /// files around - one of them would silently win per file - and the second one only exists
    /// because somebody put it there, so it is theirs to deal with.
    /// </remarks>
    [Fact]
    public void AnOldFolderBesideANewOneIsNotMerged()
    {
        WriteMarker(Previous, "dlss_swapper.db", "the old one");
        WriteMarker(Current, "dlss_swapper.db", "the current one");

        var resolved = Storage.ResolveRootFolder(_root);

        Assert.Equal(Current, resolved);
        Assert.True(Directory.Exists(Previous));
        Assert.Equal("the current one", File.ReadAllText(Path.Combine(Current, "dlss_swapper.db")));
        Assert.Equal("the old one", File.ReadAllText(Path.Combine(Previous, "dlss_swapper.db")));
    }

    /// <summary>
    /// A move that cannot happen leaves the app reading its own library.
    /// </summary>
    /// <remarks>
    /// This is the case that decides whether the migration is safe. A file held open by another
    /// copy of the app, or a permission, makes the move throw - and returning the new path anyway
    /// would present an empty library to somebody whose data is right there. Returning the old path
    /// keeps them working and lets the next launch try again.
    /// </remarks>
    [Fact]
    public void AMoveThatFailsKeepsUsingTheOldFolder()
    {
        WriteMarker(Previous, "dlss_swapper.db", "the library");

        var held = Path.Combine(Previous, "held-open.bin");
        using (var _ = new FileStream(held, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            var resolved = Storage.ResolveRootFolder(_root);

            // Either the platform refused the move - the case being tested - or it allowed it, in
            // which case the data still arrived and the guarantee holds anyway. Asserting on which
            // would be asserting on Windows rather than on this code.
            if (Directory.Exists(Previous))
            {
                Assert.Equal(Previous, resolved);
                Assert.Equal("the library", File.ReadAllText(Path.Combine(Previous, "dlss_swapper.db")));
            }
            else
            {
                Assert.Equal(Current, resolved);
                Assert.Equal("the library", File.ReadAllText(Path.Combine(Current, "dlss_swapper.db")));
            }
        }
    }

    /// <summary>
    /// Whatever happened, it is written down for whoever can log it later.
    /// </summary>
    [Fact]
    public void TheOutcomeIsRecorded()
    {
        WriteMarker(Previous, "dlss_swapper.db", "the library");

        Storage.ResolveRootFolder(_root);

        Assert.NotNull(Storage.MigrationNote);
        Assert.Contains(Storage.CurrentFolderName, Storage.MigrationNote);
    }
}
