using DLSS_Swapper.Data.GitHub;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Whether a GitHub release has already been offered to the user.
/// </summary>
/// <remarks>
/// The rule the update dialog states where it writes the setting: do not prompt again for this
/// version or lower. It was implemented as the inverse, so closing one update dialog silently
/// suppressed every future release for good. Nothing tested it, and the everyday case - the latest
/// release being the one already prompted for - gives the right answer either way, which is why it
/// could sit there.
/// </remarks>
public class UpdatePromptTests
{
    [Fact]
    public void ANewerReleaseThanTheOneLastOfferedIsStillOffered()
    {
        Assert.False(GitHubUpdater.HasPromptedBefore(thisVersion: 1_002_000, lastVersionPromptedFor: 1_001_000));
    }

    [Fact]
    public void TheReleaseAlreadyOfferedIsNotOfferedAgain()
    {
        Assert.True(GitHubUpdater.HasPromptedBefore(thisVersion: 1_001_000, lastVersionPromptedFor: 1_001_000));
    }

    /// <summary>An older release than the one already offered is not news either.</summary>
    [Fact]
    public void AnOlderReleaseIsNotOffered()
    {
        Assert.True(GitHubUpdater.HasPromptedBefore(thisVersion: 1_000_000, lastVersionPromptedFor: 1_001_000));
    }

    /// <summary>
    /// Nobody has been prompted yet, so everything is news. No special case needed: a real release
    /// always encodes above zero.
    /// </summary>
    [Fact]
    public void WithNothingEverOfferedTheReleaseIsOffered()
    {
        Assert.False(GitHubUpdater.HasPromptedBefore(thisVersion: 1, lastVersionPromptedFor: 0));
    }

    /// <summary>
    /// A release whose name does not parse encodes to zero, and must not be announced as an update.
    /// </summary>
    [Fact]
    public void AnUnparseableReleaseIsNotOffered()
    {
        Assert.True(GitHubUpdater.HasPromptedBefore(thisVersion: 0, lastVersionPromptedFor: 0));
    }
}
