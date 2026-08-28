using System;
using System.Collections.Generic;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers turning undone swaps into the sentence the games page shows.
/// </summary>
/// <remarks>
/// The dismissal rule is the part that has to be exact: closing the bar acknowledges what it
/// showed and nothing after it. A flag instead of a high-water mark would silence every future
/// undone swap the first time the bar was ever closed.
/// </remarks>
public class UndoneSwapNoticeTests
{
    static readonly DateTime _base = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    static UndoneSwap Undone(string gameId, int minute, GameAssetType assetType = GameAssetType.DLSS)
    {
        return new UndoneSwap()
        {
            GameId = gameId,
            AssetType = assetType,
            ChangedAt = _base.AddMinutes(minute),
        };
    }

    static readonly IReadOnlyDictionary<string, string> _titles = new Dictionary<string, string>()
    {
        ["g1"] = "First Game",
        ["g2"] = "Second Game",
    };

    [Fact]
    public void NothingUndoneMeansNoNotice()
    {
        Assert.Null(UndoneSwapNotice.For(new List<UndoneSwap>(), _titles, DateTime.MinValue));
    }

    [Fact]
    public void OneUndoneSwapNamesTheGameAndTheDll()
    {
        var notice = UndoneSwapNotice.For(new[] { Undone("g1", 10) }, _titles, DateTime.MinValue);

        Assert.NotNull(notice);
        Assert.Equal(ResourceHelper.GetString("UndoneSwaps_TitleOne"), notice.Title);
        Assert.Contains("First Game", notice.Message);
        Assert.Contains(ResourceHelper.GetString("General_Name_DLSS"), notice.Message);
        Assert.Equal(_base.AddMinutes(10), notice.NewestChangedAt);
    }

    [Fact]
    public void SeveralUndoneSwapsCountThemselvesAndCarryTheNewestTime()
    {
        var notice = UndoneSwapNotice.For(
            new[] { Undone("g1", 10), Undone("g2", 30, GameAssetType.XeSS) },
            _titles,
            DateTime.MinValue);

        Assert.NotNull(notice);
        Assert.Contains("2", notice.Title);
        Assert.Contains("First Game", notice.Message);
        Assert.Contains("Second Game", notice.Message);
        Assert.Equal(_base.AddMinutes(30), notice.NewestChangedAt);
    }

    [Fact]
    public void DismissingSilencesExactlyWhatWasShown()
    {
        var shown = UndoneSwapNotice.For(new[] { Undone("g1", 10) }, _titles, DateTime.MinValue);
        Assert.NotNull(shown);

        // Same state, read again after dismissing: quiet.
        Assert.Null(UndoneSwapNotice.For(new[] { Undone("g1", 10) }, _titles, shown.NewestChangedAt));

        // A swap undone after the dismissal speaks again.
        var later = UndoneSwapNotice.For(
            new[] { Undone("g1", 10), Undone("g2", 20) },
            _titles,
            shown.NewestChangedAt);
        Assert.NotNull(later);
        Assert.Equal(ResourceHelper.GetString("UndoneSwaps_TitleOne"), later.Title);
        Assert.Contains("Second Game", later.Message);
        Assert.DoesNotContain("First Game", later.Message);
    }

    [Fact]
    public void AGameNoLongerInTheLibraryIsLeftOut()
    {
        // Uninstalled since the swap was undone. There is no row on the page to act on, so a line
        // about it would be an instruction with nowhere to go.
        Assert.Null(UndoneSwapNotice.For(new[] { Undone("gone", 10) }, _titles, DateTime.MinValue));
    }
}
