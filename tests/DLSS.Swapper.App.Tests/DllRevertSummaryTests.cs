using DLSS_Swapper.Helpers;
using DLSS_Swapper.UserControls;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers the sentence a revert run ends on.
/// </summary>
/// <remarks>
/// The summary builds its resource key by concatenation, which is exactly how the one-dll case
/// came to resolve "DllRevert_RevertedTemplateOne" — a key nothing defined — and show a resource
/// error in the very case that had been singled out for better wording. These tests run each
/// count through the real resources, so a key that does not exist fails here instead of on
/// screen.
/// </remarks>
public class DllRevertSummaryTests
{
    [Fact]
    public void OneDllInOneGameGetsItsOwnSentence()
    {
        var text = DllUpdatePrompt.SummaryTextFor("DllRevert_Reverted", swapped: 1, gamesUpdated: 1);

        Assert.Equal(ResourceHelper.GetString("DllRevert_RevertedOne"), text);
        Assert.DoesNotContain("LangResourceError", text);
    }

    [Fact]
    public void SeveralDllsInOneGameDoNotCountTheGame()
    {
        var text = DllUpdatePrompt.SummaryTextFor("DllRevert_Reverted", swapped: 4, gamesUpdated: 1);

        // "Restored 4 dlls across 1 games" was the sentence a play-clean session ended on.
        Assert.Contains("4", text);
        Assert.DoesNotContain("1 game", text);
        Assert.DoesNotContain("LangResourceError", text);
    }

    [Fact]
    public void ManyGamesCountBothWays()
    {
        var text = DllUpdatePrompt.SummaryTextFor("DllRevert_Reverted", swapped: 5, gamesUpdated: 3);

        Assert.Contains("5", text);
        Assert.Contains("3", text);
        Assert.DoesNotContain("LangResourceError", text);
    }
}
