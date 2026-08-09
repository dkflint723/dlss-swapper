using System.Linq;
using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the per type slots that replaced Game's eighteen named properties.
/// </summary>
[Collection(ManifestCollection.Name)]
public class GameSlotTests
{
    static GameAsset Asset(string gameId, GameAssetType assetType, string path, string version = "1.0.0.0")
    {
        return new GameAsset()
        {
            Id = gameId,
            AssetType = assetType,
            Path = path,
            Version = version,
            Size = 1024,
            Hash = string.Empty,
        };
    }

    [Fact]
    public void EverySwappableTypeHasASlot()
    {
        var game = new TestGame("slots_1");

        foreach (var dllTypeDefinition in DllTypes.All)
        {
            Assert.NotNull(game.GetAssetSlot(dllTypeDefinition.AssetType));
        }

        Assert.Equal(DllTypes.All.Length, game.AssetSlots.Count);
    }

    [Fact]
    public void BackupTypesDoNotGetASlot()
    {
        var game = new TestGame("slots_2");

        // A backup is a copy of the original dll, not something the game can be swapped to, so it
        // has no slot of its own.
        Assert.Null(game.GetAssetSlot(GameAssetType.DLSS_BACKUP));
    }

    [Fact]
    public void ASlotIsEmptyUntilTheGameHasThatDll()
    {
        var game = new TestGame("slots_3");

        var slot = game.GetAssetSlot(GameAssetType.DLSS);

        Assert.NotNull(slot);
        Assert.Null(slot!.CurrentAsset);
        Assert.False(slot.MultipleFound);
    }

    [Fact]
    public void AnInstalledDllFillsItsOwnSlotOnly()
    {
        using var manifest = new ManifestScope();
        var game = new TestGame("slots_4");
        var dlss = Asset(game.ID, GameAssetType.DLSS, @"C:\game\nvngx_dlss.dll");
        game.GameAssets.Add(dlss);

        game.UpdateCurrentDLLsFromGameAssets();

        Assert.Same(dlss, game.GetAssetSlot(GameAssetType.DLSS)!.CurrentAsset);
        Assert.Null(game.GetAssetSlot(GameAssetType.DLSS_G)!.CurrentAsset);
        Assert.Null(game.GetAssetSlot(GameAssetType.XeSS)!.CurrentAsset);
    }

    [Fact]
    public void TheSameDllInTwoPlacesIsReportedAsMultipleFound()
    {
        using var manifest = new ManifestScope();
        var game = new TestGame("slots_5");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, @"C:\game\bin\nvngx_dlss.dll"));
        var second = Asset(game.ID, GameAssetType.DLSS, @"C:\game\bin2\nvngx_dlss.dll");
        game.GameAssets.Add(second);

        game.UpdateCurrentDLLsFromGameAssets();

        var slot = game.GetAssetSlot(GameAssetType.DLSS)!;
        Assert.True(slot.MultipleFound);

        // Last one wins, which is what the chain of assignments this replaced did.
        Assert.Same(second, slot.CurrentAsset);
    }

    [Fact]
    public void OneCopyIsNotReportedAsMultipleFound()
    {
        using var manifest = new ManifestScope();
        var game = new TestGame("slots_6");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, @"C:\game\nvngx_dlss.dll"));

        game.UpdateCurrentDLLsFromGameAssets();

        Assert.False(game.GetAssetSlot(GameAssetType.DLSS)!.MultipleFound);
    }

    [Fact]
    public void RemovingADllClearsItsSlot()
    {
        using var manifest = new ManifestScope();
        var game = new TestGame("slots_7");
        var dlss = Asset(game.ID, GameAssetType.DLSS, @"C:\game\nvngx_dlss.dll");
        game.GameAssets.Add(dlss);
        game.UpdateCurrentDLLsFromGameAssets();

        game.GameAssets.Remove(dlss);
        game.UpdateCurrentDLLsFromGameAssets();

        Assert.Null(game.GetAssetSlot(GameAssetType.DLSS)!.CurrentAsset);
    }

    [Fact]
    public void SlotsCoverEveryTypeTheRegistryKnowsAbout()
    {
        var game = new TestGame("slots_8");

        // If a tenth dll type is added to the registry it gets a slot for free. This is the property
        // the refactor was for, so it is worth asserting rather than assuming.
        var slotTypes = game.AssetSlots.Select(x => x.AssetType).OrderBy(x => x);
        var registryTypes = DllTypes.All.Select(x => x.AssetType).OrderBy(x => x);

        Assert.Equal(registryTypes, slotTypes);
    }
}
