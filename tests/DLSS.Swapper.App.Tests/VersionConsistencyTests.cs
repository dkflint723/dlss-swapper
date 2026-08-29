using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace DLSS_Swapper.App.Tests;

/// <summary>
/// Every place this fork writes its version down agrees with every other place.
/// </summary>
/// <remarks>
/// <para>
/// The version lives in four files by hand, and three of them were missed. The 1.2.6.0 release
/// shipped an installer whose file properties, and the entry it wrote into Add or remove programs,
/// both still said 1.2.5.1 - so the thing a user would look at to know what they had installed was
/// naming a different release. Nothing caught it because nothing compared them.
/// </para>
/// <para>
/// This reads the real files out of the repository rather than anything generated, so it fails on a
/// developer's machine at the moment of the mistake rather than in a release that has already gone
/// out.
/// </para>
/// </remarks>
public class VersionConsistencyTests
{
    /// <summary>
    /// The repository root, found by walking up from the test assembly.
    /// </summary>
    /// <remarks>
    /// The solution file is the marker. Tests run from bin/, at a depth that changes with the
    /// configuration and target framework, so counting directories up would be its own thing to get
    /// wrong.
    /// </remarks>
    static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "DLSS Swapper.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not find the repository root from " + AppContext.BaseDirectory);
        }
    }

    static string Read(params string[] parts) => File.ReadAllText(Path.Combine(RepositoryRoot, Path.Combine(parts)));

    static string Match(string content, string pattern, string what)
    {
        var match = Regex.Match(content, pattern);

        Assert.True(match.Success, $"Could not find the version in {what}. The file's shape changed, so this test is no longer reading it.");

        return match.Groups[1].Value;
    }

    /// <summary>The number the app reports, which everything else is checked against.</summary>
    static string ProjectVersion => Match(
        Read("src", "DLSS Swapper.csproj"),
        @"<Version>([^<]+)</Version>",
        "src/DLSS Swapper.csproj");

    [Fact]
    public void ThePackagingScriptBuildsTheVersionTheAppReports()
    {
        // This one names the installer and the zip, so a mismatch ships files whose names disagree
        // with what is inside them.
        var packaged = Match(Read("package", "config.cmd"), @"set app_version=(\S+)", "package/config.cmd");

        Assert.Equal(ProjectVersion, packaged);
    }

    [Fact]
    public void TheApplicationManifestMatches()
    {
        var manifest = Match(
            Read("src", "app.manifest"),
            @"<assemblyIdentity version=""([^""]+)""",
            "src/app.manifest");

        Assert.Equal(ProjectVersion, manifest);
    }

    /// <summary>
    /// The command line, which is installed beside the app and carries its own version.
    /// </summary>
    /// <remarks>
    /// It has no version of its own to report - it is the same release as the app it sits next to,
    /// and it exists to be driven by things that will be asked which build they are talking to. The
    /// sdk defaults to 1.0.0.0 when nothing is set, and that is precisely what the first packaged
    /// build of it shipped: a 1.0.0.0 executable in the install folder of a 2.2.2.0 app. It went
    /// unnoticed because until that release it was not packaged at all.
    /// </remarks>
    [Fact]
    public void TheCommandLineMatches()
    {
        var cli = Match(
            Read("cli", "DLSS.Swapper.Cli", "DLSS.Swapper.Cli.csproj"),
            @"<Version>([^<]+)</Version>",
            "cli/DLSS.Swapper.Cli/DLSS.Swapper.Cli.csproj");

        Assert.Equal(ProjectVersion, cli);
    }

    /// <summary>
    /// Every version the installer stamps, including the one a user reads in Add or remove programs.
    /// </summary>
    [Fact]
    public void EveryVersionTheInstallerStampsMatches()
    {
        var installer = Read("package", "NSIS", "Installer.nsi");

        var stamped = new (string Pattern, string What)[]
        {
            (@"!define MUI_VERSION ""([^""]+)""", "MUI_VERSION"),
            (@"VIProductVersion ""([^""]+)""", "VIProductVersion"),
            (@"VIAddVersionKey ""ProductVersion"" ""([^""]+)""", "ProductVersion"),
            (@"VIAddVersionKey ""FileVersion"" ""([^""]+)""", "FileVersion"),
            (@"""DisplayVersion"" ""([^""]+)""", "the uninstall entry's DisplayVersion"),
        };

        foreach (var (pattern, what) in stamped)
        {
            Assert.Equal(ProjectVersion, Match(installer, pattern, $"Installer.nsi ({what})"));
        }
    }

    /// <summary>
    /// Four numeric parts, none of them huge.
    /// </summary>
    /// <remarks>
    /// GitHubRelease.GetVersionNumber parses a release name into four ulongs and packs them at 16
    /// bits each. A part above 65535 silently overflows into the part above it, and a semver style
    /// suffix - 2.0.0-fork.3 - fails TryParse and returns 0, which reads as "no version" and stops
    /// the app ever seeing an update. So the scheme is not free to become prettier later.
    /// </remarks>
    [Fact]
    public void TheVersionIsFourNumbersTheUpdaterCanRead()
    {
        var parts = ProjectVersion.Split('.');

        Assert.Equal(4, parts.Length);

        foreach (var part in parts)
        {
            Assert.True(ushort.TryParse(part, out _), $"'{part}' is not a number the updater can pack into 16 bits.");
        }
    }

    /// <summary>
    /// This fork's own line, kept clear of upstream's.
    /// </summary>
    /// <remarks>
    /// It used to be upstream's version with this fork's count in the fourth part, which collided
    /// the day upstream released a 1.2.6.0 of its own: two builds wearing one number, and this
    /// fork's reading as older than an upstream release it contained all of.
    /// </remarks>
    [Fact]
    public void TheVersionDoesNotReuseUpstreamsLine()
    {
        var major = int.Parse(ProjectVersion.Split('.')[0]);

        Assert.True(major >= 2, $"Version {ProjectVersion} is back in upstream's 1.x range, where the two can collide.");
    }
}
