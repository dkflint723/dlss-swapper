using System.Linq;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the "update available" badge: whether a game is behind the newest dll it could swap to.
/// </summary>
[Collection(ManifestCollection.Name)]
public class GameUpdateAvailableTests
{
    static GameAsset Asset(string gameId, GameAssetType assetType, string version, string hash = "")
    {
        return new GameAsset()
        {
            Id = gameId,
            AssetType = assetType,
            Path = $@"C:\game\{assetType}.dll",
            Version = version,
            Size = 1024,
            Hash = hash,
        };
    }

    [Fact]
    public void AGameBehindTheNewestDllReportsAnUpdate()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("update_1");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));

        game.RefreshUpdateAvailable();

        Assert.True(game.UpdateAvailable);
        Assert.Contains(GameAssetType.DLSS, game.OutdatedAssetTypes);
    }

    [Fact]
    public void AGameOnTheNewestDllReportsNoUpdate()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("update_2");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.7.0.0"));

        game.RefreshUpdateAvailable();

        Assert.False(game.UpdateAvailable);
        Assert.Empty(game.OutdatedAssetTypes);
    }

    [Fact]
    public void AGameAheadOfTheManifestReportsNoUpdate()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.1.0.0");

        var game = new TestGame("update_3");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.7.0.0"));

        game.RefreshUpdateAvailable();

        Assert.False(game.UpdateAvailable);
    }

    [Fact]
    public void NoManifestMeansNoUpdate()
    {
        // On a cold start games load from cache before the manifest arrives. Reporting every game
        // as up to date is right here; RefreshUpdateAvailable runs again once the manifest loads.
        using var manifest = new ManifestScope();

        var game = new TestGame("update_4");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));

        game.RefreshUpdateAvailable();

        Assert.False(game.UpdateAvailable);
    }

    [Fact]
    public void ADllWithAnUnreadableVersionIsLeftOutRatherThanGuessedAt()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("update_5");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, string.Empty));

        game.RefreshUpdateAvailable();

        Assert.False(game.UpdateAvailable);
    }

    [Fact]
    public void OnlyTheOutdatedTypesAreReported()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.DLSS_G, "310.7.0.0");

        var game = new TestGame("update_6");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_G, "310.7.0.0"));

        game.RefreshUpdateAvailable();

        Assert.Equal(new[] { GameAssetType.DLSS }, game.OutdatedAssetTypes);
    }

    [Fact]
    public void OutdatedDllsFromOneVendorProduceOneBadge()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.DLSS_G, "310.7.0.0");
        manifest.Add(GameAssetType.DLSS_D, "310.7.0.0");

        var game = new TestGame("update_7");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_G, "310.1.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS_D, "310.1.0.0"));

        game.RefreshUpdateAvailable();

        // Three outdated dlls, but one vendor, so one badge rather than three identical ones.
        Assert.Equal(3, game.OutdatedAssetTypes.Count);
        var update = Assert.Single(game.AvailableUpdates);
        Assert.Equal(DllVendor.Nvidia, update.Vendor);

        // The badge only says which vendor, so the tooltip is the only place the three dlls are
        // named. It resolves through the resource map, which reports failures as a sentinel string
        // rather than throwing, so a badge with no readable text would otherwise pass unnoticed.
        Assert.False(string.IsNullOrWhiteSpace(update.Label));
        Assert.DoesNotContain("LangResourceError", update.Label);
        Assert.DoesNotContain("LangResourceError", update.ToolTip);
    }

    [Fact]
    public void OutdatedDllsFromTwoVendorsProduceTwoBadges()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.XeSS, "2.1.0.0");

        var game = new TestGame("update_8");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS, "2.0.0.0"));

        game.RefreshUpdateAvailable();

        Assert.Equal(2, game.AvailableUpdates.Count);
        Assert.Contains(game.AvailableUpdates, x => x.Vendor == DllVendor.Nvidia);
        Assert.Contains(game.AvailableUpdates, x => x.Vendor == DllVendor.Intel);
    }

    [Fact]
    public void FsrIsRankedBySdkVersionNotFileVersion()
    {
        // The bug this covers: a later 3.1.2 build ships as file version 1.0.2.38022, which is
        // numerically above 3.1.4's 1.0.1.41314. Ranking FSR by file version put the older sdk on
        // top and told a game on 3.1.4 that it was out of date.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.FSR_31_DX12, "1.0.2.38022", internalName: "3.1.2");
        manifest.Add(GameAssetType.FSR_31_DX12, "1.0.1.41314", internalName: "3.1.4", md5Hash: "FSR314");

        var game = new TestGame("update_9");

        // Ranked by file version, 3.1.2's 1.0.2.38022 would be the newest and this game would be
        // told it is behind. Ranked by sdk version it is already on the newest.
        game.GameAssets.Add(Asset(game.ID, GameAssetType.FSR_31_DX12, "1.0.1.41314", hash: "FSR314"));

        game.RefreshUpdateAvailable();

        Assert.False(game.UpdateAvailable);
    }

    [Fact]
    public void ADllRecognisedByHashIsRankedByTheManifestNotTheFile()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");
        manifest.Add(GameAssetType.DLSS, "310.1.0.0", md5Hash: "KNOWNHASH");

        var game = new TestGame("update_10");

        // The file on disk claims a version above everything in the manifest, but its hash matches a
        // known 310.1 record, so the manifest is believed over the file.
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "999.0.0.0", hash: "KNOWNHASH"));

        game.RefreshUpdateAvailable();

        Assert.True(game.UpdateAvailable);
    }

    [Fact]
    public void RefreshingTwiceDoesNotAccumulate()
    {
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("update_11");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, "310.1.0.0"));

        game.RefreshUpdateAvailable();
        game.RefreshUpdateAvailable();

        Assert.Single(game.AvailableUpdates);
        Assert.Single(game.OutdatedAssetTypes);
    }

    [Fact]
    public void SwappingToTheNewestDllClearsTheBadge()
    {
        // The bug this covers: the badge was computed on load and never again, so a game kept
        // showing "update available" after it had just been updated.
        using var manifest = new ManifestScope();
        manifest.Add(GameAssetType.DLSS, "310.7.0.0");

        var game = new TestGame("update_12");
        var dlss = Asset(game.ID, GameAssetType.DLSS, "310.1.0.0");
        game.GameAssets.Add(dlss);
        game.RefreshUpdateAvailable();
        Assert.True(game.UpdateAvailable);

        dlss.Version = "310.7.0.0";
        game.RefreshUpdateAvailable();

        Assert.False(game.UpdateAvailable);
        Assert.Empty(game.AvailableUpdates);
    }
}
