using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>
/// The dll types a game has, and the ones it does not.
/// </summary>
/// <remarks>
/// The game page used to show all nine types whatever the game shipped, so most of it read "Not
/// found". The absent ones are still worth stating, because "this game has no frame generation" is
/// an answer, but they belong in one line rather than in nine controls.
/// </remarks>
public class GameEngineSplit
{
    /// <summary>Types this game actually has a dll for, in registry order.</summary>
    public required IReadOnlyList<GameAssetType> Present { get; init; }

    /// <summary>Types it does not.</summary>
    public required IReadOnlyList<GameAssetType> Absent { get; init; }

    /// <summary>Reads as "5 upscalers not in this game — FSR 3.1 Vulkan, XeSS FG, …".</summary>
    public required string AbsentSummary { get; init; }
}

public static class GameEngines
{
    public static GameEngineSplit Split(Game game)
    {
        var present = new List<GameAssetType>();
        var absent = new List<GameAssetType>();

        // Registry order rather than the order the files happened to be found in, so two games with
        // the same dlls list them the same way.
        foreach (var dllTypeDefinition in DllTypes.All)
        {
            var hasIt = game.GameAssets.Any(x => x.AssetType == dllTypeDefinition.AssetType);

            if (hasIt)
            {
                present.Add(dllTypeDefinition.AssetType);
            }
            else
            {
                absent.Add(dllTypeDefinition.AssetType);
            }
        }

        return new GameEngineSplit()
        {
            Present = present,
            Absent = absent,
            AbsentSummary = DescribeAbsent(absent),
        };
    }

    static string DescribeAbsent(IReadOnlyList<GameAssetType> absent)
    {
        if (absent.Count == 0)
        {
            return string.Empty;
        }

        var names = string.Join(", ", absent.Select(x => DLLManager.Instance.GetAssetTypeName(x)));

        // Its own sentence rather than a count and a plural s, because the template rendered
        // "1 upscalers not in this game" for a game missing exactly one.
        if (absent.Count == 1)
        {
            return ResourceHelper.GetFormattedResourceTemplate("GamePage_NotPresentOneTemplate", names);
        }

        return ResourceHelper.GetFormattedResourceTemplate(
            "GamePage_NotPresentTemplate",
            absent.Count,
            names);
    }
}
