using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Versioning;
using Xunit;

namespace DLSS_Swapper.Tests;

public class DllVersionRankingTests
{
    // Real values from the shipped manifest. FSR 3.1.4 has a lower file version than a later
    // 3.1.2 build, which is the whole reason this class exists.
    const string Fsr314Internal = "3.1.4";
    const string Fsr314File = "1.0.1.41314";
    const string Fsr312Internal = "3.1.2";
    const string Fsr312LaterFile = "1.0.2.38022";

    static ulong Rank(GameAssetType assetType, string? internalVersion, string? fileVersion)
    {
        Assert.True(DllVersionRanking.TryGetRank(assetType, internalVersion, fileVersion, out var rank));
        return rank;
    }

    /// <summary>
    /// The case that was reporting games as up to date while a newer FSR was available.
    /// </summary>
    [Theory]
    [InlineData(GameAssetType.FSR_31_DX12)]
    [InlineData(GameAssetType.FSR_31_VK)]
    public void Fsr_RanksBySdkVersionEvenWhenTheFileVersionDisagrees(GameAssetType assetType)
    {
        var newerSdk = Rank(assetType, Fsr314Internal, Fsr314File);
        var olderSdkNewerFile = Rank(assetType, Fsr312Internal, Fsr312LaterFile);

        Assert.True(newerSdk > olderSdkNewerFile);
    }

    [Theory]
    [InlineData(GameAssetType.FSR_31_DX12_BACKUP)]
    [InlineData(GameAssetType.FSR_31_VK_BACKUP)]
    public void Fsr_BackupsRankTheSameWayAsTheDllsTheyBackUp(GameAssetType assetType)
    {
        Assert.True(Rank(assetType, Fsr314Internal, Fsr314File) > Rank(assetType, Fsr312Internal, Fsr312LaterFile));
    }

    /// <summary>
    /// Latent until AMD ships a double digit patch, but a string comparison would rank 3.1.10 below
    /// 3.1.4, which is how the library page's own ordering was written.
    /// </summary>
    [Fact]
    public void Fsr_RanksDoubleDigitPatchesAboveSingleDigitOnes()
    {
        Assert.True(Rank(GameAssetType.FSR_31_DX12, "3.1.10", "1.0.1.50000")
            > Rank(GameAssetType.FSR_31_DX12, "3.1.4", Fsr314File));
    }

    [Theory]
    [InlineData(GameAssetType.DLSS)]
    [InlineData(GameAssetType.DLSS_G)]
    [InlineData(GameAssetType.XeSS)]
    [InlineData(GameAssetType.XeLL)]
    public void EverythingElse_RanksByFileVersionAndIgnoresAnyInternalVersion(GameAssetType assetType)
    {
        // A misleading internal version must not change the ranking for these types.
        Assert.True(Rank(assetType, "1.0.0", "310.7.0.0") > Rank(assetType, "99.0.0", "310.6.0.0"));
    }

    [Fact]
    public void Fsr_FallsBackToFileVersionWhenNoSdkVersionIsKnown()
    {
        // Imported dlls often have no internal name recorded.
        Assert.True(Rank(GameAssetType.FSR_31_DX12, null, "1.0.2.38022")
            > Rank(GameAssetType.FSR_31_DX12, string.Empty, "1.0.1.41314"));
    }

    [Fact]
    public void TryGetRank_FailsWhenNeitherVersionIsUsable()
    {
        Assert.False(DllVersionRanking.TryGetRank(GameAssetType.DLSS, null, null, out var rank));
        Assert.Equal(0UL, rank);

        Assert.False(DllVersionRanking.TryGetRank(GameAssetType.FSR_31_DX12, "not.a.version", "also.bad", out rank));
        Assert.Equal(0UL, rank);
    }

    [Fact]
    public void OnlyFsr_RanksByItsInternalVersion()
    {
        Assert.All(DllTypes.All, x =>
            Assert.Equal(x.Vendor == DllVendor.Amd, x.VersionFromInternalName));
    }
}
