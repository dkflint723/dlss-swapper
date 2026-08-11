using DLSS_Swapper.Data;
using DLSS_Swapper.Dlls;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Covers what the upscalers page's search matches, and the empty state when it matches nothing.
/// </summary>
/// <remarks>
/// One predicate, because the list on the right and the count beside each engine on the left are
/// the same control: the number is printed on the button that opens the list.
/// </remarks>
public class DllSearchTests
{
    /// <summary>
    /// DisplayName is computed from these rather than settable, so the tests drive it the way the
    /// app does — which also means they are testing the string a row actually shows.
    /// </summary>
    static DLLRecord Record(string version, string label = "", string hash = "", string internalName = "", bool isDevFile = false)
    {
        return new DLLRecord()
        {
            AssetType = GameAssetType.DLSS,
            Version = version,
            AdditionalLabel = label,
            MD5Hash = hash,
            InternalName = internalName,
            IsDevFile = isDevFile,
        };
    }

    [Fact]
    public void AnEmptyQueryMatchesEverything()
    {
        Assert.True(DllSearch.Matches(Record("3.7.20.0"), null));
        Assert.True(DllSearch.Matches(Record("3.7.20.0"), string.Empty));
        Assert.True(DllSearch.Matches(Record("3.7.20.0"), "   "));
    }

    [Fact]
    public void TheVersionIsSearchableInBothFormsItIsShownIn()
    {
        // DisplayName is what the row shows; Version is what a file's properties in Windows show,
        // and they differ because the display form drops trailing zero groups.
        var record = Record("3.7.20.0");

        Assert.True(DllSearch.Matches(record, "3.7.20"));
        Assert.True(DllSearch.Matches(record, "3.7.20.0"));
    }

    [Fact]
    public void TheVariantAndCodenameInTheNameAreSearchable()
    {
        // Two builds of one version are told apart only by this, so it has to be findable.
        Assert.True(DllSearch.Matches(Record("3.7.20.0", label: "v2"), "(v2)"));
        Assert.True(DllSearch.Matches(Record("2.2.18.0", label: "Beta White Collie 1"), "collie"));
    }

    [Fact]
    public void AHashMatchesFromItsStartOnly()
    {
        var record = Record("3.7.20.0", hash: "ABC123DEF456");

        Assert.True(DllSearch.Matches(record, "abc1"));

        // Any two characters appear somewhere in nearly every hash on the page, so a substring
        // match would return the whole list and look like the search was ignored.
        Assert.False(DllSearch.Matches(record, "123DEF"));
    }

    [Fact]
    public void DebugFilesStayOptIn()
    {
        var debug = Record("3.7.20.0", isDevFile: true);

        Assert.False(DllSearch.Passes(debug, "3.7.20", allowDebugDlls: false));
        Assert.True(DllSearch.Passes(debug, "3.7.20", allowDebugDlls: true));
    }

    [Fact]
    public void TheCountAndTheListUseTheSameRule()
    {
        var records = new[]
        {
            Record("3.7.20.0"),
            Record("3.7.10.0"),
            Record("3.7.20.0", isDevFile: true),
        };

        // The count read the raw collection while the list hid debug files, so DLSS said 107 over
        // a list of 88. Both go through this now.
        Assert.Equal(2, DllSearch.Count(records, null, allowDebugDlls: false));
        Assert.Equal(3, DllSearch.Count(records, null, allowDebugDlls: true));
        Assert.Equal(1, DllSearch.Count(records, "3.7.10", allowDebugDlls: false));
    }

    [Fact]
    public void ASearchThatMatchesNothingSaysWhereTheMatchesAre()
    {
        var state = UpscalersEmptyState.For(
            visibleCount: 0, engineTotal: 108, engineName: "DLSS", searchText: "2.1", matchesElsewhere: 4);

        Assert.Equal(UpscalersEmptyStateKind.NoSearchResults, state.Kind);
        Assert.Contains("DLSS", state.Title);
        Assert.Contains("2.1", state.Title);

        // The dead end has to become a next step, and the page already counted where to go.
        Assert.Contains("4", state.Body);
        Assert.Contains("108", state.PrimaryLabel);
    }

    [Fact]
    public void ASearchMatchingNowhereSaysSoInsteadOfPointingElsewhere()
    {
        var state = UpscalersEmptyState.For(
            visibleCount: 0, engineTotal: 108, engineName: "DLSS", searchText: "zzzz", matchesElsewhere: 0);

        Assert.Equal(UpscalersEmptyStateKind.NoSearchResults, state.Kind);
        Assert.DoesNotContain("LangResourceError", state.Body);
        Assert.False(string.IsNullOrWhiteSpace(state.Body));
    }

    [Fact]
    public void AnEngineWithNoVersionsIsNotASearchProblem()
    {
        // Checked after the search, for the same reason the games page checks it after: a search
        // that matched nothing says nothing about whether the engine has versions at all.
        var state = UpscalersEmptyState.For(
            visibleCount: 0, engineTotal: 0, engineName: "XeLL", searchText: string.Empty, matchesElsewhere: 0);

        Assert.Equal(UpscalersEmptyStateKind.NoVersions, state.Kind);
        Assert.Contains("XeLL", state.Title);
    }

    [Fact]
    public void AListWithRowsInItIsNotEmpty()
    {
        var state = UpscalersEmptyState.For(
            visibleCount: 3, engineTotal: 108, engineName: "DLSS", searchText: "310", matchesElsewhere: 0);

        Assert.Equal(UpscalersEmptyStateKind.None, state.Kind);
    }
}
