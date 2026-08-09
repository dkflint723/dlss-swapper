using System.Threading.Tasks;
using DLSS_Swapper.Data;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// A concrete Game for tests, since Game itself is abstract.
/// </summary>
/// <remarks>
/// Reports as a manually added game because that library has the fewest expectations about where
/// the game came from.
/// </remarks>
internal class TestGame : Game
{
    public override GameLibrary GameLibrary => GameLibrary.ManuallyAdded;

    public override bool IsReadyToPlay => true;

    public TestGame(string id)
    {
        ID = id;
        Title = id;
        PlatformId = id;
    }

    protected override Task UpdateCacheImageAsync() => Task.CompletedTask;

    public override bool UpdateFromGame(Game game) => ParentUpdateFromGame(game);
}
