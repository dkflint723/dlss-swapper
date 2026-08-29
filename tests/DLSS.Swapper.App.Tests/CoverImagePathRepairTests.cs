using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper;
using DLSS_Swapper.Data.ManuallyAdded;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Pointing a stored cover path at the image cache the app is using now.
/// </summary>
/// <remarks>
/// <para>
/// Cover paths are stored absolute. 3.0.0.0 renamed the data folder from "DLSS Swapper" to
/// "Swapshelf", the art moved with the folder, and every stored path went on naming the folder that
/// had just stopped existing. Nothing failed loudly: a cover that cannot be found reads as a game
/// that has no cover, so the app quietly fell back to the store's own art - 240x280 for two of the
/// games this was found on, against the 600x900 sitting unused in the cache - and the library looked
/// like it had lost its covers.
/// </para>
/// <para>
/// The repair re-derives from <see cref="Storage.GetImageCachePath"/> rather than rewriting the old
/// name out of the string, so a later rename needs no new code and a library copied between machines
/// heals the same way.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public class CoverImagePathRepairTests
{
    static string WriteCover(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[64]);
        return path;
    }

    /// <summary>A path under a data folder that no longer exists, which is what the rename left.</summary>
    static string StalePath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "swapshelf-gone", "DLSS Swapper", "image_cache", fileName);

    [Fact]
    public async Task ACustomCoverIsFoundAgainAfterTheDataFolderIsRenamed()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var game = new ManuallyAddedGame("repair-custom");
        var custom = WriteCover(game.ExpectedCustomCoverImage);
        game.CoverImage = StalePath(Path.GetFileName(custom));

        Assert.True(game.RepairCoverImagePath());
        Assert.Equal(custom, game.CoverImage);
    }

    [Fact]
    public async Task TheCustomCoverWinsOverTheDownloadedOne()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var game = new ManuallyAddedGame("repair-prefers-custom");
        var custom = WriteCover(game.ExpectedCustomCoverImage);
        WriteCover(game.ExpectedCoverImage);
        game.CoverImage = StalePath(Path.GetFileName(custom));

        // The same order LoadCoverImageAsync uses, so a repaired row lands on the file the app
        // would have chosen anyway. Getting this backwards would replace somebody's chosen art
        // with the store's, which is the bug wearing a different hat.
        Assert.True(game.RepairCoverImagePath());
        Assert.Equal(custom, game.CoverImage);
    }

    [Fact]
    public async Task ADownloadedCoverIsUsedWhenThereIsNoCustomOne()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var game = new ManuallyAddedGame("repair-standard");
        var standard = WriteCover(game.ExpectedCoverImage);
        game.CoverImage = StalePath(Path.GetFileName(standard));

        Assert.True(game.RepairCoverImagePath());
        Assert.Equal(standard, game.CoverImage);
    }

    [Fact]
    public async Task AStalePathWithNothingBehindItIsCleared()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var game = new ManuallyAddedGame("repair-nothing");
        game.CoverImage = StalePath("repair-nothing_custom_400_600.png");

        // Cleared rather than left pointing at nothing, which is what lets the normal fetch run.
        Assert.True(game.RepairCoverImagePath());
        Assert.Null(game.CoverImage);
    }

    [Fact]
    public async Task APathAlreadyInTheCurrentCacheIsLeftAlone()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var game = new ManuallyAddedGame("repair-current");
        var custom = WriteCover(game.ExpectedCustomCoverImage);
        game.CoverImage = custom;

        // No write, so the row is not touched on every launch for ever.
        Assert.False(game.RepairCoverImagePath());
        Assert.Equal(custom, game.CoverImage);
    }

    [Fact]
    public async Task AGameWithNoCoverIsLeftAlone()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var game = new ManuallyAddedGame("repair-none");
        game.CoverImage = null;

        Assert.False(game.RepairCoverImagePath());
        Assert.Null(game.CoverImage);
    }

    [Fact]
    public async Task TheRepairedPathIsWrittenToTheDatabase()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var game = new ManuallyAddedGame("repair-persists")
        {
            Title = "Repair Persists",
            InstallPath = database.GameFolder,
        };
        var custom = WriteCover(game.ExpectedCustomCoverImage);
        game.CoverImage = StalePath(Path.GetFileName(custom));
        await game.SaveToDatabaseAsync();

        // The whole point. Repairing in memory and not telling the row would look fixed until the
        // next launch put the stale path back, which is the shape of the original bug.
        Assert.True(game.RepairCoverImagePath());
        await game.SaveToDatabaseAsync();

        var reloaded = await Database.Instance.Connection
            .Table<ManuallyAddedGame>()
            .FirstOrDefaultAsync(row => row.ID == game.ID);

        Assert.NotNull(reloaded);
        Assert.Equal(custom, reloaded.CoverImage);
    }
}
