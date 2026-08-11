using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the filter that carries "which games are using this file" from the upscalers page to the
/// games page.
/// </summary>
/// <remarks>
/// The matching itself is <see cref="DllUsage.IsUsedBy"/> and is covered by its own tests. What is
/// worth pinning down here is that this filter asks exactly that question and no other, and that it
/// arrives with words the games page can show.
/// </remarks>
public class DllFilterTests
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

    static DllFilter FilterFor(GameAssetType assetType, string hash, string version)
    {
        return new DllFilter(assetType, hash, version, DllFilter.LabelFor("DLSS", "v310.7"));
    }

    [Fact]
    public void ItMatchesTheGamesUsingThatFileAndNoOthers()
    {
        var filter = FilterFor(GameAssetType.DLSS, "ABC123", "3.10.7");

        var using310 = GameWith("a", Asset("a", GameAssetType.DLSS, "3.10.7", "ABC123"));
        var usingSomethingElse = GameWith("b", Asset("b", GameAssetType.DLSS, "3.7.20", "DEF456"));
        var usingNothing = GameWith("c");

        Assert.True(filter.Matches(using310));
        Assert.False(filter.Matches(usingSomethingElse));
        Assert.False(filter.Matches(usingNothing));
    }

    [Fact]
    public void TheSameVersionOfADifferentUpscalerIsNotAMatch()
    {
        // Three upscalers can carry the same version number, and a filter that ignored the type
        // would answer a question nobody asked.
        var filter = FilterFor(GameAssetType.DLSS, string.Empty, "2.0.0");

        Assert.False(filter.Matches(GameWith("a", Asset("a", GameAssetType.XeSS, "2.0.0"))));
        Assert.True(filter.Matches(GameWith("b", Asset("b", GameAssetType.DLSS, "2.0.0"))));
    }

    [Fact]
    public void TheLabelSaysBothWhichUpscalerAndWhichVersion()
    {
        var label = DllFilter.LabelFor("DLSS", "v310.7");

        Assert.DoesNotContain("LangResourceError", label);

        // A version on its own does not say which upscaler, and the games page shows nothing else
        // about what it has been narrowed to.
        Assert.Contains("DLSS", label);
        Assert.Contains("v310.7", label);
    }

    [Fact]
    public void TheFilterCarriesItsOwnLabel()
    {
        // Built where the record is known and read where it is not, so the games page never has to
        // reach back to the dll list to describe what it is showing.
        var filter = FilterFor(GameAssetType.DLSS, "ABC123", "3.10.7");

        Assert.False(string.IsNullOrWhiteSpace(filter.Label));
    }
}
