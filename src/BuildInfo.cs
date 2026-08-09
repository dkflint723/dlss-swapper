
using System;
using System.Globalization;

namespace DLSS_Swapper;

internal static class BuildInfo
{
    public static string GitBranch { get; } = string.Empty;
    public static string GitCommit { get; } = string.Empty;
    public static string GitTag { get; } = string.Empty;
    public static long BuildTimestamp { get; }


    public static string GitCommitShort
    {
        get
        {
            if (string.IsNullOrWhiteSpace(GitCommit) || GitCommit.Length < 7)
            {
                return string.Empty;
            }

            return GitCommit.Substring(0, 7);
        }
    }
    public static DateTime BuildDateTime => DateTimeOffset.FromUnixTimeSeconds(BuildTimestamp).LocalDateTime;

    /// <summary>
    /// Whether the release workflow stamped this build.
    /// </summary>
    /// <remarks>
    /// The fields above are placeholders that the workflow rewrites before it compiles, so a build
    /// made anywhere else has none of them. An unstamped timestamp is zero, which is 1970, and the
    /// about page was reporting that as the build date -- a real looking answer to "what am I
    /// running" that was wrong by half a century.
    /// </remarks>
    public static bool IsStamped => BuildTimestamp > 0;

    public static string BuildDateTimeFormattedString => IsStamped
        ? BuildDateTime.ToString("g", CultureInfo.CurrentCulture)
        : string.Empty;

    public static bool IsFromTagBuild => string.IsNullOrWhiteSpace(GitTag) == false;

    /// <summary>
    /// Whether to name the commit this was built from.
    /// </summary>
    /// <remarks>
    /// Only for untagged builds, where the version number alone does not say which build this is,
    /// and only when there is actually a commit to name rather than an empty line and a button that
    /// copies nothing.
    /// </remarks>
    public static bool ShowsGitCommit => IsFromTagBuild == false && string.IsNullOrEmpty(GitCommitShort) == false;
}
