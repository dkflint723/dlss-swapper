using System.Collections.Generic;
using System.Text;

namespace DLSS_Swapper.CoverArt;

/// <summary>
/// Decides whether a search result is the game it was searched for, or only a guess at it.
/// </summary>
/// <remarks>
/// <para>
/// A scan across a whole library cannot ask about every game, so it has to say which answers it is
/// sure of. Only a confident match is ticked for the user; everything else is listed unticked, and
/// choosing it is theirs to do.
/// </para>
/// <para>
/// Deliberately strict, and deliberately not clever. "Cyberpunk 2077" against "Cyberpunk 2077:
/// Phantom Liberty" is not a match, and neither is "FINAL FANTASY VII (2013)" against "Final
/// Fantasy VII" - both are real entries a real search returns, and both are exactly the cases where
/// a person should look. Being wrong in the strict direction costs a tick box; being wrong in the
/// loose direction silently replaces the art on somebody's game with the wrong game's art.
/// </para>
/// </remarks>
public static class CoverArtMatch
{
    /// <summary>Whether the two names are the same game beyond reasonable doubt.</summary>
    public static bool IsConfident(string? libraryTitle, string? searchResultName)
    {
        var left = Normalise(libraryTitle);

        if (left.Length == 0)
        {
            return false;
        }

        return left == Normalise(searchResultName);
    }

    /// <summary>
    /// The first result that is certainly the game, or null when none of them are.
    /// </summary>
    /// <remarks>
    /// Every result is checked rather than only the top one. SteamGridDB ranks by its own
    /// popularity, not by how well a name matches, so a search for a smaller game can put a bigger
    /// one above the right answer - and taking the top result on faith is how a scan ends up
    /// putting a famous game's art on an obscure one.
    /// </remarks>
    public static CoverArtGame? FirstConfident(string? libraryTitle, IReadOnlyList<CoverArtGame> results)
    {
        foreach (var result in results)
        {
            if (IsConfident(libraryTitle, result.Name))
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// The comparable form of a title: lower case, without the punctuation and decoration two
    /// catalogues disagree about, and with its spacing collapsed.
    /// </summary>
    /// <remarks>
    /// Only differences that are never meaningful are removed. A store writing "Marvel's
    /// Spider-Man" where an art database writes "Marvel s Spider Man" is the same game; a store
    /// writing "2013" where the database does not is a question, and stays one.
    /// </remarks>
    public static string Normalise(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(title.Length);
        var lastWasSpace = true;

        foreach (var character in title)
        {
            if (char.IsLetterOrDigit(character))
            {
                _ = builder.Append(char.ToLowerInvariant(character));
                lastWasSpace = false;

                continue;
            }

            // Everything else - punctuation, trademark marks, separators - becomes one space, so
            // that "Spider-Man" and "Spider Man" land on the same string rather than different ones.
            if (lastWasSpace == false)
            {
                _ = builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }
}
