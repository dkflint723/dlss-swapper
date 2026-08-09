using DLSS_Swapper.Data;
using DLSS_Swapper.UserControls;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the view models that can be built without their control.
/// </summary>
/// <remarks>
/// Most view models here take their page or control as a constructor argument, and instantiating a
/// XAML control outside a running application throws, so they are out of reach from a test host.
/// The ones below take their data directly and are testable as written.
/// </remarks>
public class ViewModelTests
{
    static GameAsset Asset(string gameId, GameAssetType assetType, string path)
    {
        return new GameAsset()
        {
            Id = gameId,
            AssetType = assetType,
            Path = path,
            Version = "310.1.0.0",
            Size = 1024,
            Hash = string.Empty,
        };
    }

    [Fact]
    public void MultipleDllsFoundListsOnlyTheRequestedType()
    {
        var game = new TestGame("vm_1");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, @"C:\game\bin\nvngx_dlss.dll"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, @"C:\game\bin2\nvngx_dlss.dll"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS, @"C:\game\bin\libxess.dll"));

        var viewModel = new MultipleDLLsFoundControlModel(game, GameAssetType.DLSS);

        Assert.Equal(2, viewModel.DLLsList.Count);
        Assert.All(viewModel.DLLsList, x => Assert.Equal(GameAssetType.DLSS, x.AssetType));
    }

    [Fact]
    public void MultipleDllsFoundKeepsEveryPath()
    {
        // The whole point of the dialog is telling the user which copies exist, so the paths have to
        // survive rather than being collapsed to one entry per type.
        var game = new TestGame("vm_2");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, @"C:\game\bin\nvngx_dlss.dll"));
        game.GameAssets.Add(Asset(game.ID, GameAssetType.DLSS, @"C:\game\bin2\nvngx_dlss.dll"));

        var viewModel = new MultipleDLLsFoundControlModel(game, GameAssetType.DLSS);

        Assert.Contains(viewModel.DLLsList, x => x.Path == @"C:\game\bin\nvngx_dlss.dll");
        Assert.Contains(viewModel.DLLsList, x => x.Path == @"C:\game\bin2\nvngx_dlss.dll");
    }

    [Fact]
    public void MultipleDllsFoundIsEmptyWhenTheGameHasNoneOfThatType()
    {
        var game = new TestGame("vm_3");
        game.GameAssets.Add(Asset(game.ID, GameAssetType.XeSS, @"C:\game\libxess.dll"));

        var viewModel = new MultipleDLLsFoundControlModel(game, GameAssetType.DLSS);

        Assert.Empty(viewModel.DLLsList);
    }
}
