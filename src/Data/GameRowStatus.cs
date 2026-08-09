using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>What a game row is telling the user to do, if anything.</summary>
public enum GameRowState
{
    UpToDate,
    HasUpdates,
    NoBackup,
    Swapping,
    UpdatesSkipped,
}

/// <summary>
/// The sentence, glyph and button for one game row.
/// </summary>
/// <remarks>
/// The redesign's central change: a row says what is true in words rather than showing a version
/// delta. "310.1 to 310.7" means nothing without knowing which is newer and whether that matters;
/// "DLSS has a newer version" is the same fact in a form that needs no decoding.
/// </remarks>
public class GameRowStatus
{
    public required GameRowState State { get; init; }

    /// <summary>The full sentence, already localised.</summary>
    public required string Sentence { get; init; }

    /// <summary>Segoe Fluent glyph, or empty when the state carries no icon.</summary>
    public required string Glyph { get; init; }

    /// <summary>True when the sentence should take the accent colour rather than plain ink.</summary>
    public required bool UsesAccent { get; init; }

    /// <summary>The row's button, or null when there is nothing to do.</summary>
    public required string? ActionLabel { get; init; }

    /// <summary>Technologies this game ships, such as "DLSS · FSR". Empty when it has none.</summary>
    public required string Engines { get; init; }

    public static GameRowStatus For(Game game)
    {
        var engines = DescribeEngines(game);

        // Being written to wins over everything: the row is mid change, so anything else it said
        // would be about a state that no longer holds.
        if (game.Processing)
        {
            return new GameRowStatus()
            {
                State = GameRowState.Swapping,
                Sentence = ResourceHelper.GetString("GamesPage_Status_Swapping"),
                Glyph = string.Empty,
                UsesAccent = false,
                ActionLabel = null,
                Engines = engines,
            };
        }

        // An update outranks a missing backup, even though the missing backup is the bigger risk.
        // Swapping saves a copy of the original before it writes, so taking the update fixes both,
        // and offering "Save a copy" here would be the slower route to the same place. Only when
        // the game is not locked, because then the swap is refused and that route does not exist.
        if (game.SkipUpdates == false && game.AvailableUpdates.Count > 0)
        {
            var names = game.AvailableUpdates.Select(x => x.Label).ToList();

            return new GameRowStatus()
            {
                State = GameRowState.HasUpdates,
                Sentence = names.Count == 1
                    ? ResourceHelper.GetFormattedResourceTemplate("GamesPage_Status_NewerVersionOne", names[0])
                    : ResourceHelper.GetFormattedResourceTemplate("GamesPage_Status_NewerVersionMany", JoinNames(names)),
                Glyph = "\uE74A",
                UsesAccent = true,
                ActionLabel = ResourceHelper.GetString("GamesPage_Action_Update"),
                Engines = engines,
            };
        }

        if (IsMissingABackup(game))
        {
            return new GameRowStatus()
            {
                State = GameRowState.NoBackup,
                Sentence = ResourceHelper.GetString("GamesPage_Status_NoBackup"),
                Glyph = "\uE7BA",
                UsesAccent = false,
                ActionLabel = ResourceHelper.GetString("GamesPage_Action_SaveACopy"),
                Engines = engines,
            };
        }

        // After the missing backup check, because saving a copy is not a change to the game and
        // locking one makes its original more valuable rather than less. When this came first, a
        // locked game missing its original showed no button and had no route to fixing it.
        if (game.SkipUpdates)
        {
            return new GameRowStatus()
            {
                State = GameRowState.UpdatesSkipped,
                Sentence = ResourceHelper.GetString("GamesPage_Status_UpdatesSkipped"),
                Glyph = "\uE72E",
                UsesAccent = false,
                ActionLabel = null,
                Engines = engines,
            };
        }

        return new GameRowStatus()
        {
            State = GameRowState.UpToDate,
            Sentence = ResourceHelper.GetString("GamesPage_Status_UpToDate"),

            // Up to date is the absence of a mark rather than a green tick, which keeps a library
            // that is mostly fine visually quiet.
            Glyph = string.Empty,
            UsesAccent = false,
            ActionLabel = null,
            Engines = engines,
        };
    }

    /// <summary>
    /// Joins names as "DLSS and FSR" or "DLSS, FSR and XeSS".
    /// </summary>
    /// <remarks>
    /// Built from a resource template rather than hardcoded, since the separator and the final
    /// conjunction differ by language.
    /// </remarks>
    internal static string JoinNames(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            return string.Empty;
        }

        if (names.Count == 1)
        {
            return names[0];
        }

        var separator = ResourceHelper.GetString("General_ListSeparator");
        var lastSeparator = ResourceHelper.GetString("General_ListFinalSeparator");

        var head = string.Join(separator, names.Take(names.Count - 1));
        return head + lastSeparator + names[names.Count - 1];
    }

    /// <summary>The technologies present, by vendor, so DLSS and its variants read as one.</summary>
    static string DescribeEngines(Game game)
    {
        var vendors = new List<DllVendor>();
        foreach (var gameAsset in game.GameAssets)
        {
            if (DllTypes.ForAssetType(gameAsset.AssetType) is null)
            {
                // A backup, or a type we do not manage.
                continue;
            }

            var vendor = DLLManager.Instance.GetAssetVendor(gameAsset.AssetType);
            if (vendor != DllVendor.Unknown && vendors.Contains(vendor) == false)
            {
                vendors.Add(vendor);
            }
        }

        vendors.Sort();

        return string.Join(" · ", vendors.Select(x => DLLManager.Instance.GetVendorShortName(x)));
    }

    /// <summary>Whether any swappable dll in this game has no backup of its type.</summary>
    static bool IsMissingABackup(Game game)
    {
        foreach (var gameAsset in game.GameAssets)
        {
            var definition = DllTypes.ForAssetType(gameAsset.AssetType);
            if (definition is null)
            {
                continue;
            }

            var hasBackup = game.GameAssets.Any(x => x.AssetType == definition.BackupAssetType);
            if (hasBackup == false)
            {
                return true;
            }
        }

        return false;
    }
}
