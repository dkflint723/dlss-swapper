using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Data;

/// <summary>
/// Why a game's DLSS preset can or cannot be set.
/// </summary>
/// <remarks>
/// Split out of the game page's model because that model reaches <c>NVAPIHelper.Instance</c>
/// directly, and <c>NVAPIHelper</c> has a private constructor, no reset, and P/Invokes at
/// construction — so nothing that touches it can be reached from a test. The decision is three
/// booleans and a sentence; only the booleans need a driver.
///
/// The three reasons were distinguishable in the model all along and were collapsed into the single
/// word "Not supported", beside a dropdown that had greyed itself out and an error icon that
/// appeared for exactly one of the three. A disabled control with no reason reads as broken rather
/// than as unavailable.
/// </remarks>
public static class PresetAvailability
{
    /// <param name="driverSupportsPresets">Whether NVAPI is present and usable at all.</param>
    /// <param name="hasPermissionIssue">Whether the driver refused access to the profile.</param>
    /// <param name="hasGameProfile">Whether the driver has a profile for this game to set on.</param>
    public static string Describe(bool driverSupportsPresets, bool hasPermissionIssue, bool hasGameProfile)
    {
        // Checked first because it is the one that is not about this game: with no NVIDIA driver
        // there is nothing to have a profile in, and saying "no profile for this game" would send
        // someone looking for a per-game fix that does not exist.
        if (driverSupportsPresets == false)
        {
            return ResourceHelper.GetString("GamePage_Preset_NoDriver");
        }

        // Before the profile check, because a refusal is why the profile could not be read. The
        // profile may well exist.
        if (hasPermissionIssue)
        {
            return ResourceHelper.GetString("GamePage_Preset_PermissionIssue");
        }

        if (hasGameProfile == false)
        {
            return ResourceHelper.GetString("GamePage_Preset_NoProfile");
        }

        return ResourceHelper.GetString("GamePage_Preset_Desc");
    }

    /// <summary>Whether the dropdown should be usable, from the same three facts.</summary>
    public static bool CanSet(bool driverSupportsPresets, bool hasPermissionIssue, bool hasGameProfile)
    {
        return driverSupportsPresets && hasPermissionIssue == false && hasGameProfile;
    }

    /// <summary>
    /// Whether a chosen preset is worth writing to the driver.
    /// </summary>
    /// <remarks>
    /// Its own function because the answer was buried in the middle of a property-changed handler,
    /// written out three times — once per preset kind — with the driver call, the failure check,
    /// the rollback and the error dialog all in the same block. Three copies of a guard is three
    /// chances for one of them to be subtly different, and none of it could be run in a test.
    ///
    /// A write that is refused rolls the dropdown back, so the guard matters twice: it is also what
    /// stops the rollback itself being taken for a new choice and written straight back.
    /// </remarks>
    public static bool ShouldWrite(bool canSet, uint? chosen, uint? current)
    {
        if (canSet == false)
        {
            return false;
        }

        if (chosen is null)
        {
            return false;
        }

        return chosen != current;
    }
}
