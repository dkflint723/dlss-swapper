using System.Collections.Generic;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the usage count on the upscalers page: how many games have a given dll in place.
/// </summary>
/// <remarks>
/// It is the number someone reads before deleting a file, so counting a game that is not using it
/// is the harmless kind of wrong and missing one that is is not.
/// </remarks>
public class DllUsageTests
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

    static TestGame GameWith(string id, params GameAsset[] assets)
    {
        var game = new TestGame(id);
        game.GameAssets.AddRange(assets);
        return game;
    }

    [Fact]
    public void AGameWithTheSameHashIsUsingIt()
    {
        var game = GameWith("usage_1", Asset("usage_1", GameAssetType.DLSS, "310.1.0.0", "abc123"));

        Assert.True(DllUsage.IsUsedBy(GameAssetType.DLSS, "abc123", "310.1.0.0", game));
    }

    [Fact]
    public void HashBeatsVersionWhenBothAreKnown()
    {
        // Two builds can carry the same file version and different contents. When the hash is known
        // on both sides it is the answer, so a version that happens to match must not override it.
        var game = GameWith("usage_2", Asset("usage_2", GameAssetType.DLSS, "310.1.0.0", "aaa"));

        Assert.False(DllUsage.IsUsedBy(GameAssetType.DLSS, "bbb", "310.1.0.0", game));
    }

    [Fact]
    public void VersionIsUsedWhenTheGameHasNoHash()
    {
        // A dll a game shipped with is often not in the manifest and was recorded without a hash.
        // Refusing to match it would report popular versions as used by nobody.
        var game = GameWith("usage_3", Asset("usage_3", GameAssetType.DLSS, "310.1.0.0"));

        Assert.True(DllUsage.IsUsedBy(GameAssetType.DLSS, "abc123", "310.1.0.0", game));
    }

    [Fact]
    public void ADifferentEngineIsNotUsingIt()
    {
        var game = GameWith("usage_4", Asset("usage_4", GameAssetType.XeSS, "310.1.0.0", "abc123"));

        Assert.False(DllUsage.IsUsedBy(GameAssetType.DLSS, "abc123", "310.1.0.0", game));
    }

    [Fact]
    public void ADifferentVersionIsNotUsingIt()
    {
        var game = GameWith("usage_5", Asset("usage_5", GameAssetType.DLSS, "310.1.0.0"));

        Assert.False(DllUsage.IsUsedBy(GameAssetType.DLSS, string.Empty, "310.7.0.0", game));
    }

    [Fact]
    public void EachGameIsCountedOnceEvenWithSeveralCopies()
    {
        // A game can ship the same dll in more than one folder. That is one game to close, not two.
        var game = GameWith(
            "usage_6",
            Asset("usage_6", GameAssetType.DLSS, "310.1.0.0"),
            Asset("usage_6", GameAssetType.DLSS, "310.1.0.0"));

        var games = new List<Game>() { game };

        Assert.Equal(1, DllUsage.CountGamesUsing(GameAssetType.DLSS, string.Empty, "310.1.0.0", games));
    }

    [Fact]
    public void CountsAcrossTheLibrary()
    {
        var games = new List<Game>()
        {
            GameWith("usage_7a", Asset("usage_7a", GameAssetType.DLSS, "310.1.0.0")),
            GameWith("usage_7b", Asset("usage_7b", GameAssetType.DLSS, "310.1.0.0")),
            GameWith("usage_7c", Asset("usage_7c", GameAssetType.DLSS, "310.7.0.0")),
            GameWith("usage_7d"),
        };

        Assert.Equal(2, DllUsage.CountGamesUsing(GameAssetType.DLSS, string.Empty, "310.1.0.0", games));
        Assert.Equal(1, DllUsage.CountGamesUsing(GameAssetType.DLSS, string.Empty, "310.7.0.0", games));
        Assert.Equal(0, DllUsage.CountGamesUsing(GameAssetType.DLSS, string.Empty, "999.0.0.0", games));
    }

    [Fact]
    public void NothingUsingItSaysSoInWords()
    {
        // "0 games" reads like a number that failed to load, and this is the number someone checks
        // before deleting a file. The answer that matters gets a word.
        var describedAsUnused = DllUsage.DescribeCount(0);

        Assert.DoesNotContain("0", describedAsUnused);
        Assert.False(string.IsNullOrWhiteSpace(describedAsUnused));
    }

    [Fact]
    public void OneGameIsNotDescribedAsGames()
    {
        Assert.DoesNotContain("1 games", DllUsage.DescribeCount(1));
        Assert.Contains("14", DllUsage.DescribeCount(14));
    }
}
