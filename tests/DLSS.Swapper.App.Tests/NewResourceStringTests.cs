using DLSS_Swapper.Helpers;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Every resource key added for the redesign resolves.
/// </summary>
/// <remarks>
/// The resource map reports a miss as the sentinel string rather than throwing, so a key that was
/// added to the code and not to the .resw renders as "LangResourceError..." in the window and
/// nothing else notices. These are the keys this work introduced, in one place, so adding a string
/// in code without adding the string fails here rather than in front of a user.
/// </remarks>
public class NewResourceStringTests
{
    [Theory]
    [InlineData("Preview_TitleTemplate")]
    [InlineData("Preview_TitleOneGameTemplate")]
    [InlineData("Preview_TitleOneFile")]
    [InlineData("Preview_Body")]
    [InlineData("Preview_CloseGamesFirst")]
    [InlineData("Preview_ConfirmTemplate")]
    [InlineData("Preview_ConfirmOneFile")]
    [InlineData("Update_StopAfterThisOne")]
    [InlineData("Update_Stopping")]
    [InlineData("Update_ProgressTemplate")]
    [InlineData("Update_DoneTemplate")]
    [InlineData("Update_DoneOneFileTemplate")]
    [InlineData("Update_DoneNothing")]
    [InlineData("Update_DonePartialTemplate")]
    [InlineData("Update_DoneReassurance")]
    [InlineData("Update_UndoAll")]
    [InlineData("Update_SeeWhatFailed")]
    [InlineData("Update_Undoing")]
    [InlineData("Update_UndoneTemplate")]
    [InlineData("Upscalers_NotUsed")]
    [InlineData("Upscalers_UsedByOneGame")]
    [InlineData("Upscalers_UsedByGamesTemplate")]
    [InlineData("General_Open")]
    public void TheStringResolves(string resourceKey)
    {
        var value = ResourceHelper.GetString(resourceKey);

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.DoesNotContain("LangResourceError", value);
    }
}
