using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Swapping;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers what a game row says.
/// </summary>
/// <remarks>
/// This is the redesign's central change, so it gets the same scrutiny as the swap path. A row that
/// says the wrong thing is worse than one that says nothing: the whole point is that the user can
/// act on the sentence without decoding a version number.
/// </remarks>
[Collection(ManifestCollection.Name)]
public class GameRowStatusTests
{
    static GameAsset Asset(string gameId, GameAssetType assetType, string version)
    {
        return new GameAsset()
        {
            Id = gameId,
            AssetType = assetType,
            Path = BackupAwarePath(assetType),
            Version = version,
            Size = 1024,
            Hash = string.Empty,
        };
    }

    /// <summary>A game with the given dlls, each backed up, refreshed against the manifest.</summary>
    static TestGame GameWith(string id, params (GameAssetType AssetType, string Version)[] installed)
    {
        var game = new TestGame(id);
        foreach (var dll in installed)
        {
            game.GameAssets.Add(Asset(game.ID, dll.AssetType, dll.Version));
            game.GameAssets.Add(Asset(game.ID, DllTypes.ForAssetType(dll.AssetType)!.BackupAssetType, dll.Version));
        }

        game.RefreshUpdateAvailable();
        return game;
    }

    [Fact]
    public void AGameWithNothingToDoSaysUpToDate()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var status = GameRowStatus.For(GameWith("row_1", (GameAssetType.DLSS, "310.7.0.0")));

        Assert.Equal(GameRowState.UpToDate, status.State);
        Assert.Null(status.ActionLabel);
        Assert.DoesNotContain("LangResourceError", status.Sentence);
    }

    [Fact]
    public void UpToDateCarriesNoGlyph()
    {
        // Absence of a mark rather than a green tick, so a library that is mostly fine stays quiet.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var status = GameRowStatus.For(GameWith("row_2", (GameAssetType.DLSS, "310.7.0.0")));

        Assert.Equal(string.Empty, status.Glyph);
        Assert.False(status.UsesAccent);
    }

    [Fact]
    public void OneEngineBehindNamesItInTheSingular()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var status = GameRowStatus.For(GameWith("row_3", (GameAssetType.DLSS, "310.1.0.0")));

        Assert.Equal(GameRowState.HasUpdates, status.State);
        Assert.Contains("has a newer version", status.Sentence);
        Assert.DoesNotContain("have newer versions", status.Sentence);
        Assert.True(status.UsesAccent);
        Assert.False(string.IsNullOrEmpty(status.ActionLabel));
    }

    [Fact]
    public void TwoEnginesBehindAreJoinedWithAnd()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.XeSS, "2.1.0.0");

        var status = GameRowStatus.For(GameWith(
            "row_4",
            (GameAssetType.DLSS, "310.1.0.0"),
            (GameAssetType.XeSS, "2.0.0.0")));

        Assert.Contains(" and ", status.Sentence);
        Assert.Contains("have newer versions", status.Sentence);
        Assert.DoesNotContain(",", status.Sentence);
    }

    [Fact]
    public void NoBackupIsReportedWhenNothingIsBehind()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("row_5");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.7.0.0"));
        game.RefreshUpdateAvailable();

        var status = GameRowStatus.For(game);

        Assert.Equal(GameRowState.NoBackup, status.State);
        Assert.False(status.UsesAccent);
        Assert.False(string.IsNullOrEmpty(status.ActionLabel));
    }

    [Fact]
    public void AnUpdateOutranksAMissingBackup()
    {
        // Swapping saves a copy of the original before it writes, so taking the update fixes both.
        // Offering "Save a copy" here would be the slower route to the same place.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("row_6");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.RefreshUpdateAvailable();

        Assert.Equal(GameRowState.HasUpdates, GameRowStatus.For(game).State);
    }

    [Fact]
    public void BeingWrittenToOutranksEverything()
    {
        // Anything else the row could say is about a state that no longer holds.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = GameWith("row_7", (GameAssetType.DLSS, "310.1.0.0"));
        game.Processing = true;

        var status = GameRowStatus.For(game);

        Assert.Equal(GameRowState.Swapping, status.State);
        Assert.Null(status.ActionLabel);
    }

    [Fact]
    public void TheSwappingSentenceSaysTheOriginalIsKept()
    {
        // The reassurance is the point of the sentence, not decoration.
        using var manifest = new ManifestScope();

        var game = new TestGame("row_8");
        game.Processing = true;

        Assert.Contains("copy of the original", GameRowStatus.For(game).Sentence);
    }

    [Fact]
    public void NoVersionNumberEverAppearsInASentence()
    {
        // The rule the redesign is built on. A delta means nothing without knowing which end is
        // newer, so no row is allowed to show one.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.XeSS, "2.1.0.0");

        var games = new List<Game>()
        {
            GameWith("row_9a", (GameAssetType.DLSS, "310.1.0.0")),
            GameWith("row_9b", (GameAssetType.DLSS, "310.7.0.0")),
            GameWith("row_9c", (GameAssetType.DLSS, "310.1.0.0"), (GameAssetType.XeSS, "2.0.0.0")),
            new TestGame("row_9d"),
        };

        foreach (var game in games)
        {
            var sentence = GameRowStatus.For(game).Sentence;

            Assert.DoesNotContain("310", sentence);
            Assert.DoesNotContain("2.0", sentence);
            Assert.DoesNotContain("2.1", sentence);
        }
    }

    [Fact]
    public void EnginesAreListedByTechnologyNotByDllType()
    {
        // A game with DLSS, frame generation and ray reconstruction ships one technology, not three,
        // so the column reads "DLSS" rather than repeating it.
        using var manifest = new ManifestScope();

        var game = new TestGame("row_10");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.7.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_G, "310.7.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_D, "310.7.0.0"));
        game.RefreshUpdateAvailable();

        var engines = GameRowStatus.For(game).Engines;

        Assert.DoesNotContain("·", engines);
        Assert.False(string.IsNullOrWhiteSpace(engines));
    }

    [Fact]
    public void TwoTechnologiesAreSeparatedWithAMiddot()
    {
        using var manifest = new ManifestScope();

        var game = new TestGame("row_11");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.7.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS, "2.1.0.0"));
        game.RefreshUpdateAvailable();

        Assert.Contains("·", GameRowStatus.For(game).Engines);
    }

    [Fact]
    public void BackupsDoNotCountAsAnEngine()
    {
        using var manifest = new ManifestScope();

        var game = new TestGame("row_12");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_BACKUP, "310.7.0.0"));
        game.RefreshUpdateAvailable();

        Assert.Equal(string.Empty, GameRowStatus.For(game).Engines);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void JoiningNamesNeverUsesAnOxfordComma(int count)
    {
        var names = new List<string>() { "DLSS", "FSR", "XeSS" }.GetRange(0, count);

        var joined = GameRowStatus.JoinNames(names);

        Assert.DoesNotContain(", and", joined);
        foreach (var name in names)
        {
            Assert.Contains(name, joined);
        }
    }

    [Fact]
    public void JoiningNothingGivesNothing()
    {
        Assert.Equal(string.Empty, GameRowStatus.JoinNames(new List<string>()));
    }

    /// <summary>Where an asset of this type would actually sit on disk.</summary>
    /// <remarks>
    /// See the note in Asset: a backup is always the dll it shadows plus ".dlsss", so a fixture that
    /// invents a path for it is describing something that cannot exist.
    /// </remarks>
    /// <summary>
    /// A game with none of the dlls this app swaps is not "up to date".
    /// </summary>
    /// <remarks>
    /// Steam runtimes, engines and tools sit in the library forever and will never ship an upscaler.
    /// Telling somebody DLSS is current in a game that has no DLSS is a claim about a thing that is
    /// not there.
    /// </remarks>
    [Fact]
    public void AGameWithNoUpscalersSaysSoRatherThanUpToDate()
    {
        var game = new TestGame("row_no_upscalers")
        {
            LastScannedAt = System.DateTime.UtcNow,
        };

        var status = GameRowStatus.For(game);

        Assert.Equal(GameRowState.NoUpscalers, status.State);
        Assert.Equal(string.Empty, status.Engines);
    }

    /// <summary>
    /// Before the first scan an empty asset list means "not looked at yet", not "has none".
    /// </summary>
    [Fact]
    public void AGameNobodyHasScannedYetDoesNotClaimToHaveNoUpscalers()
    {
        var game = new TestGame("row_unscanned");

        Assert.Null(game.LastScannedAt);
        Assert.NotEqual(GameRowState.NoUpscalers, GameRowStatus.For(game).State);
    }

    /// <summary>Locking a game with nothing in it still reports the nothing.</summary>
    [Fact]
    public void AGameWithNoUpscalersSaysSoEvenWhenUpdatesAreTurnedOff()
    {
        var game = new TestGame("row_no_upscalers_locked")
        {
            LastScannedAt = System.DateTime.UtcNow,
            SkipUpdates = true,
        };

        Assert.Equal(GameRowState.NoUpscalers, GameRowStatus.For(game).State);
    }

    static string BackupAwarePath(GameAssetType assetType)
    {
        var shadowed = DllTypes.All.FirstOrDefault(x => x.BackupAssetType == assetType);

        return shadowed is null
            ? $@"C:\game\{assetType}.dll"
            : DllSwapExecutor.GetBackupPath($@"C:\game\{shadowed.AssetType}.dll");
    }
}
