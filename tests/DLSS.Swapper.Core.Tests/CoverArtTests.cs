using System.Linq;
using DLSS_Swapper.CoverArt;
using Xunit;

namespace DLSS_Swapper.Tests;

/// <summary>
/// The rules behind the cover art picker. The json in these tests is the shape SteamGridDB actually
/// returned, trimmed to the fields that are read.
/// </summary>
public class CoverArtTests
{
    #region What we ask for

    /// <summary>
    /// The app has one 400x600 portrait to fill, so the horizontal capsules must never be asked
    /// for. This is a guard on that: they are a one word edit away from being added to the list.
    /// </summary>
    [Theory]
    [InlineData("460x215")]
    [InlineData("920x430")]
    [InlineData("512x512")]
    [InlineData("1024x1024")]
    public void PortraitDimensions_AsksForNothingThatIsNotAPortrait(string dimension)
    {
        Assert.DoesNotContain(dimension, CoverArtQuery.PortraitDimensions);
    }

    /// <summary>The exact 2:3 goes first, because it is the one that fits without any letterboxing.</summary>
    [Fact]
    public void PortraitDimensions_AsksForTheExactFitFirst()
    {
        Assert.StartsWith("600x900", CoverArtQuery.PortraitDimensions);
    }

    /// <summary>
    /// Static and only static. SteamGridDB's animated grids are webp and apng, and the covers here
    /// are drawn by a BitmapImage, which animates gif and nothing else - so an animated grid would
    /// be flattened to one frame and shown as a still. There is deliberately no option for it.
    /// </summary>
    [Fact]
    public void PortraitQuery_OnlyEverAsksForStaticArt()
    {
        Assert.Contains("types=static", CoverArtQuery.PortraitQuery());
        Assert.DoesNotContain("animated", CoverArtQuery.PortraitQuery());
    }

    /// <summary>
    /// Flagged art is off with no way to turn it on. "any" merely permits it and "true" returns
    /// *only* it - both are one word from here, so both are asserted against rather than just the
    /// one that looks dangerous.
    /// </summary>
    [Fact]
    public void PortraitQuery_AlwaysExcludesNsfw()
    {
        var query = CoverArtQuery.PortraitQuery();

        Assert.Contains("nsfw=false", query);
        Assert.DoesNotContain("nsfw=any", query);
        Assert.DoesNotContain("nsfw=true", query);
    }

    [Fact]
    public void PortraitQuery_AsksForThePortraitSizesAndNothingElse()
    {
        Assert.Contains($"dimensions={CoverArtQuery.PortraitDimensions}", CoverArtQuery.PortraitQuery());
    }

    [Theory]
    [InlineData("Halo™ Infinite", "Halo Infinite")]
    [InlineData("DOOM®", "DOOM")]
    [InlineData("  Portal  2  ", "Portal 2")]
    [InlineData("Game©", "Game")]
    public void SearchTermFor_DropsStoreDecorationAndTheGapsItLeaves(string title, string expected)
    {
        Assert.Equal(expected, CoverArtQuery.SearchTermFor(title));
    }

    /// <summary>
    /// Subtitles and edition suffixes are left alone on purpose. Cutting them turns one accurate
    /// result into a page of near misses, and the list is picked from by name anyway.
    /// </summary>
    [Theory]
    [InlineData("DOOM 3: BFG Edition")]
    [InlineData("The Witcher 3: Wild Hunt - Game of the Year Edition")]
    public void SearchTermFor_KeepsEverythingElse(string title)
    {
        Assert.Equal(title, CoverArtQuery.SearchTermFor(title));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void SearchTermFor_HandlesNothing(string? title, string expected)
    {
        Assert.Equal(expected, CoverArtQuery.SearchTermFor(title));
    }

    #endregion

    #region The list picked from

    const string SearchResponse = """
        {"success":true,"data":[
          {"id":2460,"name":"Doom","verified":true,"types":["gog"],"release_date":740102400},
          {"id":8103,"name":"Doom","verified":true,"types":["steam"],"release_date":1465948800},
          {"id":9001,"name":"No Date","verified":false,"types":[],"release_date":null}
        ]}
        """;

    /// <summary>
    /// Two games named exactly "Doom" is the case this list exists for, so the year and the stores
    /// have to survive the read - they are the only things telling those two rows apart.
    /// </summary>
    [Fact]
    public void ReadGames_KeepsWhatTellsTwoGamesOfTheSameNameApart()
    {
        var games = CoverArtJson.ReadGames(SearchResponse);

        Assert.Equal(3, games.Count);

        Assert.Equal("Doom", games[0].Name);
        Assert.Equal(1993, games[0].ReleaseYear);
        Assert.Equal(["gog"], games[0].Stores);

        Assert.Equal("Doom", games[1].Name);
        Assert.Equal(2016, games[1].ReleaseYear);
        Assert.Equal(["steam"], games[1].Stores);
    }

    [Fact]
    public void ReadGames_AllowsAGameWithNoReleaseDate()
    {
        var games = CoverArtJson.ReadGames(SearchResponse);

        Assert.Null(games[2].ReleaseYear);
        Assert.False(games[2].Verified);
        Assert.Empty(games[2].Stores);
    }

    [Fact]
    public void ReadGames_SkipsARowWithNothingToShow()
    {
        var games = CoverArtJson.ReadGames("""
            {"success":true,"data":[
              {"id":1,"name":"","types":[]},
              {"name":"No id","types":[]},
              {"id":2,"name":"Fine","types":[]}
            ]}
            """);

        Assert.Equal("Fine", Assert.Single(games).Name);
    }

    #endregion

    #region The art offered

    const string GridResponse = """
        {"success":true,"data":[
          {"id":1,"style":"alternate","width":600,"height":900,"nsfw":false,"humor":false,"epilepsy":false,
           "url":"https://cdn2.steamgriddb.com/grid/a.png","thumb":"https://cdn2.steamgriddb.com/thumb/a.jpg",
           "author":{"name":"Strøhinja","steam64":"7656","avatar":"https://example.invalid/a.jpg"}},
          {"id":2,"style":"alternate","width":600,"height":900,"nsfw":true,"humor":false,"epilepsy":false,
           "url":"https://cdn2.steamgriddb.com/grid/b.png","thumb":"https://cdn2.steamgriddb.com/thumb/b.jpg",
           "author":{"name":"b"}},
          {"id":3,"style":"alternate","width":600,"height":900,"nsfw":false,"humor":true,"epilepsy":false,
           "url":"https://cdn2.steamgriddb.com/grid/c.png","thumb":"https://cdn2.steamgriddb.com/thumb/c.jpg",
           "author":{"name":"c"}},
          {"id":4,"style":"alternate","width":600,"height":900,"nsfw":false,"humor":false,"epilepsy":true,
           "url":"https://cdn2.steamgriddb.com/grid/d.png","thumb":"https://cdn2.steamgriddb.com/thumb/d.jpg",
           "author":{"name":"d"}},
          {"id":5,"style":"blurred","width":660,"height":930,"nsfw":false,"humor":false,"epilepsy":false,
           "url":"","thumb":"","author":{"name":"e"}}
        ]}
        """;

    /// <summary>
    /// The request excludes these too. Dropping them again here means losing that query parameter
    /// cannot on its own put flagged art in front of anyone. There is no way to turn either off.
    /// </summary>
    [Fact]
    public void ReadImages_AlwaysLeavesOutNsfw()
    {
        Assert.DoesNotContain(CoverArtJson.ReadImages(GridResponse), x => x.Id == 2);
    }

    /// <summary>The one flag the api gives no request parameter for, so this is its only filter.</summary>
    [Fact]
    public void ReadImages_AlwaysLeavesOutEpilepsyFlaggedArt()
    {
        Assert.DoesNotContain(CoverArtJson.ReadImages(GridResponse), x => x.Id == 4);
    }

    /// <summary>
    /// Joke art stays. Nobody asked for it to be hidden, and it is a fine thing to choose when you
    /// can see what you are choosing.
    /// </summary>
    [Fact]
    public void ReadImages_KeepsJokeArt()
    {
        Assert.Contains(CoverArtJson.ReadImages(GridResponse), x => x.Id == 3);
    }

    [Fact]
    public void ReadImages_TakesTheAuthorNameOutOfTheObjectItIsNestedIn()
    {
        var image = Assert.Single(CoverArtJson.ReadImages(GridResponse), x => x.Id == 1);

        Assert.Equal("Strøhinja", image.Author);
        Assert.Equal(600, image.Width);
        Assert.Equal(900, image.Height);
        Assert.Equal("alternate", image.Style);
    }

    /// <summary>A tile with no thumbnail to show and no image to apply is not worth offering.</summary>
    [Fact]
    public void ReadImages_SkipsArtWithNothingBehindIt()
    {
        Assert.DoesNotContain(CoverArtJson.ReadImages(GridResponse), x => x.Id == 5);
    }

    #endregion

    #region What went wrong

    /// <summary>
    /// Both errors a user will realistically hit are about the key they pasted into settings, so
    /// the api's own wording is worth more than anything this app could substitute for it.
    /// </summary>
    [Theory]
    [InlineData("""{"success":false,"errors":["Invalid key format"]}""", "Invalid key format")]
    [InlineData("""{"success":false,"errors":["Invalid asset dimensions specified"]}""", "Invalid asset dimensions specified")]
    [InlineData("""{"success":false,"errors":["one","two"]}""", "one, two")]
    public void ReadError_RepeatsWhatTheApiSaid(string json, string expected)
    {
        Assert.Equal(expected, CoverArtJson.ReadError(json));
    }

    [Fact]
    public void ReadError_IsNullWhenNothingWentWrong()
    {
        Assert.Null(CoverArtJson.ReadError(SearchResponse));
    }

    [Fact]
    public void ReadError_ReportsAResponseItCannotRead()
    {
        Assert.NotNull(CoverArtJson.ReadError("<html>502 Bad Gateway</html>"));
    }

    /// <summary>A body that is not json at all must come back empty rather than throwing.</summary>
    [Fact]
    public void Reading_SurvivesARubbishResponse()
    {
        Assert.Empty(CoverArtJson.ReadGames("<html>502 Bad Gateway</html>"));
        Assert.Empty(CoverArtJson.ReadImages("<html>502 Bad Gateway</html>"));
    }

    #endregion

    #region Which matches a scan may tick for you

    /// <summary>
    /// Differences two catalogues disagree about for no reason: case, punctuation, trademark marks
    /// and spacing. None of them mean a different game.
    /// </summary>
    [Theory]
    [InlineData("DOOM", "Doom")]
    [InlineData("Halo™ Infinite", "Halo Infinite")]
    [InlineData("Marvel's Spider-Man", "Marvel s Spider Man")]
    [InlineData("God of War Ragnarök", "God of War Ragnarök")]
    [InlineData("  Portal  2 ", "Portal 2")]
    public void IsConfident_IgnoresDifferencesThatAreNeverMeaningful(string libraryTitle, string resultName)
    {
        Assert.True(CoverArtMatch.IsConfident(libraryTitle, resultName));
    }

    /// <summary>
    /// The cases a person has to look at. Each of these is a real pair a real search returns, and
    /// each would put the wrong game's art on somebody's game if it were ticked for them.
    /// </summary>
    [Theory]
    [InlineData("Cyberpunk 2077", "Cyberpunk 2077: Phantom Liberty")]
    [InlineData("FINAL FANTASY VII (2013)", "Final Fantasy VII")]
    [InlineData("Doom", "DOOM II")]
    [InlineData("Cyberpunk 2077", "Cyberprank Girls 2077")]
    [InlineData("The Witcher 3", "The Witcher 3: Wild Hunt")]
    public void IsConfident_IsFalseForAnythingThatIsOnlyAGuess(string libraryTitle, string resultName)
    {
        Assert.False(CoverArtMatch.IsConfident(libraryTitle, resultName));
    }

    [Theory]
    [InlineData(null, "Doom")]
    [InlineData("", "Doom")]
    [InlineData("   ", "Doom")]
    [InlineData("Doom", null)]
    [InlineData("Doom", "")]
    public void IsConfident_IsFalseWhenThereIsNothingToCompare(string? libraryTitle, string? resultName)
    {
        Assert.False(CoverArtMatch.IsConfident(libraryTitle, resultName));
    }

    [Theory]
    [InlineData("Marvel's Spider-Man", "marvel s spider man")]
    [InlineData("DOOM: The Dark Ages", "doom the dark ages")]
    [InlineData("!!!", "")]
    public void Normalise_ReducesATitleToWhatIsWorthComparing(string title, string expected)
    {
        Assert.Equal(expected, CoverArtMatch.Normalise(title));
    }

    /// <summary>
    /// SteamGridDB ranks by its own popularity rather than by how well a name matches, so the right
    /// answer is not always first. Taking the top result on faith is how a scan puts a famous
    /// game's art onto an obscure one.
    /// </summary>
    [Fact]
    public void FirstConfident_LooksPastTheTopResult()
    {
        var results = CoverArtJson.ReadGames("""
            {"success":true,"data":[
              {"id":1,"name":"Cyberpunk 2077: Phantom Liberty","types":[]},
              {"id":2,"name":"Cyberprank Girls 2077","types":[]},
              {"id":3,"name":"Cyberpunk 2077","types":[]}
            ]}
            """);

        Assert.Equal(3, CoverArtMatch.FirstConfident("Cyberpunk 2077", results)?.Id);
    }

    [Fact]
    public void FirstConfident_IsNullWhenNothingIsCertain()
    {
        var results = CoverArtJson.ReadGames("""
            {"success":true,"data":[
              {"id":1,"name":"DOOM II","types":[]},
              {"id":2,"name":"DOOM 3: BFG Edition","types":[]}
            ]}
            """);

        Assert.Null(CoverArtMatch.FirstConfident("Doom", results));
    }

    [Fact]
    public void FirstConfident_IsNullWhenThereAreNoResultsAtAll()
    {
        Assert.Null(CoverArtMatch.FirstConfident("Doom", []));
    }

    #endregion
}
