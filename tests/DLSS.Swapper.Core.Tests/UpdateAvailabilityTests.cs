using System.Collections.Generic;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Xunit;

namespace DLSS_Swapper.Tests;

public class UpdateAvailabilityTests
{
    const ulong Old = 100;
    const ulong Current = 200;

    static IReadOnlyDictionary<GameAssetType, ulong> Latest(params GameAssetType[] assetTypes)
    {
        var latest = new Dictionary<GameAssetType, ulong>();
        foreach (var assetType in assetTypes)
        {
            latest[assetType] = Current;
        }

        return latest;
    }

    [Fact]
    public void ReportsATypeWhoseInstalledDllIsBehind()
    {
        var outdated = UpdateAvailability.FindOutdatedTypes(
            [new InstalledDll(GameAssetType.DLSS, Old)],
            Latest(GameAssetType.DLSS));

        Assert.Equal([GameAssetType.DLSS], outdated);
    }

    [Fact]
    public void ReportsNothingWhenTheInstalledDllIsCurrent()
    {
        var outdated = UpdateAvailability.FindOutdatedTypes(
            [new InstalledDll(GameAssetType.DLSS, Current)],
            Latest(GameAssetType.DLSS));

        Assert.Empty(outdated);
    }

    /// <summary>
    /// A game can keep the same dll in several places at different versions. A swap updates all of
    /// them, so one stale location makes the type out of date even if another is current.
    /// </summary>
    [Fact]
    public void ASingleStaleLocationMakesTheWholeTypeOutdated()
    {
        var outdated = UpdateAvailability.FindOutdatedTypes(
            [new InstalledDll(GameAssetType.DLSS, Current), new InstalledDll(GameAssetType.DLSS, Old)],
            Latest(GameAssetType.DLSS));

        Assert.Equal([GameAssetType.DLSS], outdated);
    }

    [Fact]
    public void ReportsATypeOnlyOnceHoweverManyLocationsAreBehind()
    {
        var outdated = UpdateAvailability.FindOutdatedTypes(
            [new InstalledDll(GameAssetType.XeSS, Old), new InstalledDll(GameAssetType.XeSS, Old)],
            Latest(GameAssetType.XeSS));

        Assert.Equal([GameAssetType.XeSS], outdated);
    }

    /// <summary>
    /// Not knowing of a newer version is not the same as there being none, so a type we have no
    /// records for must not be reported.
    /// </summary>
    [Fact]
    public void SkipsTypesWithNoKnownLatest()
    {
        var outdated = UpdateAvailability.FindOutdatedTypes(
            [new InstalledDll(GameAssetType.DLSS, Old), new InstalledDll(GameAssetType.XeLL, Old)],
            Latest(GameAssetType.DLSS));

        Assert.Equal([GameAssetType.DLSS], outdated);
    }

    [Fact]
    public void IgnoresTypesTheGameDoesNotHaveInstalled()
    {
        var outdated = UpdateAvailability.FindOutdatedTypes(
            [],
            Latest(GameAssetType.DLSS, GameAssetType.XeSS));

        Assert.Empty(outdated);
    }

    /// <summary>
    /// The result drives a row of badges, so it must not reorder itself depending on the order the
    /// game's assets happened to be scanned in.
    /// </summary>
    [Fact]
    public void ReturnsTypesInRegistryOrderRegardlessOfInputOrder()
    {
        var latest = Latest(GameAssetType.DLSS, GameAssetType.XeSS, GameAssetType.FSR_31_DX12);

        var outdated = UpdateAvailability.FindOutdatedTypes(
            [
                new InstalledDll(GameAssetType.XeSS, Old),
                new InstalledDll(GameAssetType.FSR_31_DX12, Old),
                new InstalledDll(GameAssetType.DLSS, Old),
            ],
            latest);

        Assert.Equal([GameAssetType.DLSS, GameAssetType.FSR_31_DX12, GameAssetType.XeSS], outdated);
    }

    [Fact]
    public void IgnoresInstalledTypesThatAreNotSwappable()
    {
        var outdated = UpdateAvailability.FindOutdatedTypes(
            [new InstalledDll(GameAssetType.Streamline_DLSS, Old), new InstalledDll(GameAssetType.DLSS_BACKUP, Old)],
            new Dictionary<GameAssetType, ulong>
            {
                [GameAssetType.Streamline_DLSS] = Current,
                [GameAssetType.DLSS_BACKUP] = Current,
            });

        Assert.Empty(outdated);
    }

    /// <summary>A dll newer than anything the manifest knows about is not out of date.</summary>
    [Fact]
    public void DoesNotReportADllNewerThanTheLatestKnown()
    {
        var outdated = UpdateAvailability.FindOutdatedTypes(
            [new InstalledDll(GameAssetType.DLSS, Current + 1)],
            Latest(GameAssetType.DLSS));

        Assert.Empty(outdated);
    }
}
