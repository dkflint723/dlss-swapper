using System.Linq;
using DLSS_Swapper.Data;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers why a game's DLSS preset can or cannot be set.
/// </summary>
/// <remarks>
/// Three distinguishable reasons were collapsed into the one word "Not supported", beside a
/// dropdown that had greyed itself out. This is the rule that un-collapsed them, and it is testable
/// only because it takes three booleans rather than reaching for NVAPIHelper — which has a private
/// constructor, no reset, and P/Invokes when it is built.
/// </remarks>
public class PresetAvailabilityTests
{
    [Fact]
    public void EachReasonGetsItsOwnSentence()
    {
        var noDriver = PresetAvailability.Describe(driverSupportsPresets: false, hasPermissionIssue: false, hasGameProfile: false);
        var refused = PresetAvailability.Describe(driverSupportsPresets: true, hasPermissionIssue: true, hasGameProfile: true);
        var noProfile = PresetAvailability.Describe(driverSupportsPresets: true, hasPermissionIssue: false, hasGameProfile: false);
        var available = PresetAvailability.Describe(driverSupportsPresets: true, hasPermissionIssue: false, hasGameProfile: true);

        foreach (var sentence in new[] { noDriver, refused, noProfile, available })
        {
            Assert.False(string.IsNullOrWhiteSpace(sentence));
            Assert.DoesNotContain("LangResourceError", sentence);
        }

        // Four states, four sentences. If any two collapse, the row is back to saying only that it
        // does not work, which is what it said before.
        var all = new[] { noDriver, refused, noProfile, available };
        Assert.Equal(all.Length, all.Distinct().Count());
    }

    [Fact]
    public void NoDriverBeatsEverythingElse()
    {
        // With no NVIDIA driver there is nothing for a profile to live in, so blaming the game
        // would send someone looking for a per-game fix that does not exist.
        var noDriver = PresetAvailability.Describe(driverSupportsPresets: false, hasPermissionIssue: false, hasGameProfile: false);

        Assert.Equal(noDriver, PresetAvailability.Describe(driverSupportsPresets: false, hasPermissionIssue: true, hasGameProfile: false));
        Assert.Equal(noDriver, PresetAvailability.Describe(driverSupportsPresets: false, hasPermissionIssue: true, hasGameProfile: true));
    }

    [Fact]
    public void ARefusalIsReportedAsARefusalNotAsAMissingProfile()
    {
        // Being refused access is why the profile could not be read. It may well exist, and
        // "no profile for this game" would be a guess presented as a fact.
        var refused = PresetAvailability.Describe(driverSupportsPresets: true, hasPermissionIssue: true, hasGameProfile: false);
        var noProfile = PresetAvailability.Describe(driverSupportsPresets: true, hasPermissionIssue: false, hasGameProfile: false);

        Assert.NotEqual(noProfile, refused);
    }

    [Fact]
    public void ThePresetIsSettableOnlyWhenAllThreeAreTrue()
    {
        Assert.True(PresetAvailability.CanSet(driverSupportsPresets: true, hasPermissionIssue: false, hasGameProfile: true));

        Assert.False(PresetAvailability.CanSet(driverSupportsPresets: false, hasPermissionIssue: false, hasGameProfile: true));
        Assert.False(PresetAvailability.CanSet(driverSupportsPresets: true, hasPermissionIssue: true, hasGameProfile: true));
        Assert.False(PresetAvailability.CanSet(driverSupportsPresets: true, hasPermissionIssue: false, hasGameProfile: false));
    }

    [Fact]
    public void ChoosingWhatIsAlreadySetWritesNothing()
    {
        // The dropdown is assigned during construction to show what the driver already has. Without
        // this, opening a game would write its own current preset straight back to the driver.
        Assert.False(PresetAvailability.ShouldWrite(canSet: true, chosen: 3, current: 3));
        Assert.True(PresetAvailability.ShouldWrite(canSet: true, chosen: 4, current: 3));
    }

    [Fact]
    public void ADisabledDropdownNeverWrites()
    {
        // When presets cannot be set the dropdown is filled with a single "Not supported" option
        // and selected, which is a property change like any other.
        Assert.False(PresetAvailability.ShouldWrite(canSet: false, chosen: 4, current: 3));
    }

    [Fact]
    public void NothingChosenWritesNothing()
    {
        Assert.False(PresetAvailability.ShouldWrite(canSet: true, chosen: null, current: 3));

        // Including the case where the game has no preset recorded either, which is what a game
        // that has never had one set looks like.
        Assert.False(PresetAvailability.ShouldWrite(canSet: true, chosen: null, current: null));
        Assert.True(PresetAvailability.ShouldWrite(canSet: true, chosen: 0, current: null));
    }

    [Fact]
    public void TheSentenceAndTheControlAgree()
    {
        // The one that matters: whenever the dropdown is disabled the row says why, and whenever it
        // is enabled the row explains what a preset is rather than an obstacle that is not there.
        var available = PresetAvailability.Describe(driverSupportsPresets: true, hasPermissionIssue: false, hasGameProfile: true);

        foreach (var driver in new[] { true, false })
        {
            foreach (var refused in new[] { true, false })
            {
                foreach (var profile in new[] { true, false })
                {
                    var canSet = PresetAvailability.CanSet(driver, refused, profile);
                    var sentence = PresetAvailability.Describe(driver, refused, profile);

                    Assert.Equal(canSet, sentence == available);
                }
            }
        }
    }
}
