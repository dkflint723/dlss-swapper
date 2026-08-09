using System;
using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers detecting a swap that a game patch has since overwritten.
/// </summary>
/// <remarks>
/// The rule has to separate three things that all look like "the dll changed": the user swapping,
/// the user reverting, and something outside the app writing over it. Only the third, landing on
/// top of a swap that was still standing, is worth telling anyone about.
/// </remarks>
public class UndoneSwapsTests
{
    static readonly DateTime _base = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    static GameHistory Event(string gameId, GameHistoryEventType eventType, int minute, GameAssetType assetType = GameAssetType.DLSS)
    {
        return new GameHistory()
        {
            GameId = gameId,
            EventType = eventType,
            AssetType = assetType,
            EventTime = _base.AddMinutes(minute),
            AssetPath = @"C:\game\nvngx_dlss.dll",
        };
    }

    [Fact]
    public void NoHistoryMeansNothingToReport()
    {
        Assert.Empty(UndoneSwapFinder.Find(new List<GameHistory>()));
    }

    [Fact]
    public void ASwapThatStillHoldsIsNotReported()
    {
        var history = new List<GameHistory>()
        {
            Event("g1", GameHistoryEventType.DLLSwapped, 10),
        };

        Assert.Empty(UndoneSwapFinder.Find(history));
    }

    [Fact]
    public void AnExternalChangeAfterASwapIsReported()
    {
        var history = new List<GameHistory>()
        {
            Event("g1", GameHistoryEventType.DLLSwapped, 10),
            Event("g1", GameHistoryEventType.DLLChangedExternally, 20),
        };

        var undone = Assert.Single(UndoneSwapFinder.Find(history));
        Assert.Equal("g1", undone.GameId);
        Assert.Equal(GameAssetType.DLSS, undone.AssetType);
        Assert.Equal(_base.AddMinutes(20), undone.ChangedAt);
    }

    [Fact]
    public void AnExternalChangeBeforeASwapIsNotReported()
    {
        // The game was patched, then the user swapped afterwards. The swap is the current state.
        var history = new List<GameHistory>()
        {
            Event("g1", GameHistoryEventType.DLLChangedExternally, 10),
            Event("g1", GameHistoryEventType.DLLSwapped, 20),
        };

        Assert.Empty(UndoneSwapFinder.Find(history));
    }

    [Fact]
    public void AnExternalChangeWithNoSwapAtAllIsNotReported()
    {
        // A game updating its own bundled dll is not a swap being undone. Reporting it would put
        // every patched game on the list forever.
        var history = new List<GameHistory>()
        {
            Event("g1", GameHistoryEventType.DLLDetected, 5),
            Event("g1", GameHistoryEventType.DLLChangedExternally, 10),
        };

        Assert.Empty(UndoneSwapFinder.Find(history));
    }

    [Fact]
    public void ADeliberateResetThenAPatchIsNotReported()
    {
        // The user put the stock dll back on purpose, so there was no swap left for the patch to
        // undo. Reporting this would tell them they lost something they chose to give up.
        var history = new List<GameHistory>()
        {
            Event("g1", GameHistoryEventType.DLLSwapped, 10),
            Event("g1", GameHistoryEventType.DLLReset, 20),
            Event("g1", GameHistoryEventType.DLLChangedExternally, 30),
        };

        Assert.Empty(UndoneSwapFinder.Find(history));
    }

    [Fact]
    public void AResetThenASwapThenAPatchIsReported()
    {
        // The reset is old news; the swap after it was standing when the patch landed.
        var history = new List<GameHistory>()
        {
            Event("g1", GameHistoryEventType.DLLReset, 10),
            Event("g1", GameHistoryEventType.DLLSwapped, 20),
            Event("g1", GameHistoryEventType.DLLChangedExternally, 30),
        };

        Assert.Single(UndoneSwapFinder.Find(history));
    }

    [Fact]
    public void ReSwappingAfterAPatchClearsIt()
    {
        // The user already dealt with it, so it must not keep appearing on the list.
        var history = new List<GameHistory>()
        {
            Event("g1", GameHistoryEventType.DLLSwapped, 10),
            Event("g1", GameHistoryEventType.DLLChangedExternally, 20),
            Event("g1", GameHistoryEventType.DLLSwapped, 30),
        };

        Assert.Empty(UndoneSwapFinder.Find(history));
    }

    [Fact]
    public void EachDllIsJudgedSeparately()
    {
        // One game can have DLSS patched over while its frame generation dll still holds.
        var history = new List<GameHistory>()
        {
            Event("g1", GameHistoryEventType.DLLSwapped, 10, GameAssetType.DLSS),
            Event("g1", GameHistoryEventType.DLLChangedExternally, 20, GameAssetType.DLSS),
            Event("g1", GameHistoryEventType.DLLSwapped, 10, GameAssetType.DLSS_G),
        };

        var undone = Assert.Single(UndoneSwapFinder.Find(history));
        Assert.Equal(GameAssetType.DLSS, undone.AssetType);
    }

    [Fact]
    public void OneGamesHistoryDoesNotAffectAnother()
    {
        var history = new List<GameHistory>()
        {
            Event("g1", GameHistoryEventType.DLLSwapped, 10),
            Event("g2", GameHistoryEventType.DLLChangedExternally, 20),
        };

        Assert.Empty(UndoneSwapFinder.Find(history));
    }

    [Fact]
    public void NewestChangeIsListedFirst()
    {
        var history = new List<GameHistory>()
        {
            Event("g1", GameHistoryEventType.DLLSwapped, 10),
            Event("g1", GameHistoryEventType.DLLChangedExternally, 20),
            Event("g2", GameHistoryEventType.DLLSwapped, 10),
            Event("g2", GameHistoryEventType.DLLChangedExternally, 40),
        };

        var undone = UndoneSwapFinder.Find(history);

        Assert.Equal(new[] { "g2", "g1" }, undone.Select(x => x.GameId));
    }

    [Fact]
    public void RowsWithoutAnAssetTypeAreIgnored()
    {
        // Some history rows are about the game rather than one dll, and have no type to judge.
        var history = new List<GameHistory>()
        {
            new GameHistory()
            {
                GameId = "g1",
                EventType = GameHistoryEventType.DLLChangedExternally,
                EventTime = _base,
                AssetType = null,
            },
        };

        Assert.Empty(UndoneSwapFinder.Find(history));
    }
}

