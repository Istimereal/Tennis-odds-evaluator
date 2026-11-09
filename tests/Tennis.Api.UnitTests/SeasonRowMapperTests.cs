using System.Text.Json;
using Tennis.Api.Import;

namespace Tennis.Api.UnitTests;

public class SeasonRowMapperTests
{
    private static readonly string[] Header =
    [
        "match_id", "season_year", "tour_type", "tour_type_human", "date_timestamp", "date_human",
        "tournament", "round", "surface", "surface_id", "status", "status_extra",
        "home_name", "away_name", "home_id", "away_id", "home_rank", "away_rank", "home_points", "away_points",
        "winner_code",
        "home_set_1_score", "away_set_1_score", "home_set_2_score", "away_set_2_score",
        "home_odds_match_winner", "away_odds_match_winner",
        "home_aces", "away_aces"
    ];

    [Fact]
    public void Map_links_players_tournament_date_and_keeps_every_column()
    {
        var plan = SeasonHeaderPlan.Classify(Header);
        var row = new[]
        {
            "50962420", "2021", "1", "ATP Tour", "1609859400", "05 Jan 2021",
            "Delray Beach ATP - Qualification", "Qualifier", "hard", "1", "FINISHED", "FINISHED",
            "  Galovic   V. ", "Dougaz A.", "479435", "283198", "", "120", "", "800",
            "1",
            "6", "4", "6", "3",
            "1.44", "2.62",
            "5", ""
        };

        var mapped = TennisDataRowMapper.Map(2021, plan, Header, row);

        Assert.True(mapped.IsValid);
        Assert.Equal("50962420", mapped.SourceKey);
        Assert.Equal("50962420", mapped.MatchId);
        Assert.StartsWith("sha256:2021:", mapped.LegacySourceKey);
        Assert.Equal(new DateOnly(2021, 1, 5), mapped.PlayedOn);
        Assert.Equal("Delray Beach ATP - Qualification", mapped.TournamentName);
        Assert.Equal("hard", mapped.Surface);
        Assert.Equal("Qualifier", mapped.Round);
        Assert.Equal("ATP Tour", mapped.TourLevel);
        Assert.Equal("6-4 6-3", mapped.Score);
        Assert.Equal("479435", mapped.Home.SourceId);
        Assert.Equal("Galovic V.", mapped.Home.Name);
        Assert.Null(mapped.Home.Ranking);
        Assert.Equal("283198", mapped.Away.SourceId);
        Assert.Equal("Dougaz A.", mapped.Away.Name);
        Assert.Equal(120, mapped.Away.Ranking);
        Assert.Contains("\"match_winner\":\"1.44\"", mapped.Home.OddsJson);
        Assert.Contains("\"aces\":\"5\"", mapped.Home.StatisticsJson);
        Assert.DoesNotContain("home_name", mapped.Home.StatisticsJson);
        Assert.Equal(["tour_type", "surface_id", "status", "status_extra"], plan.UnmappedColumns);

        using var raw = JsonDocument.Parse(mapped.RawJson);
        foreach (var column in Header)
        {
            Assert.True(raw.RootElement.TryGetProperty(column, out _), column);
        }
    }

    [Fact]
    public void Classify_keeps_player_identity_out_of_the_statistic_lists()
    {
        var plan = SeasonHeaderPlan.Classify(["winner_code", "home_name", "home_id", "home_aces", "away_odds_match_winner"]);

        Assert.Equal([3], plan.HomeIndexes);
        Assert.Empty(plan.AwayIndexes);
        Assert.Empty(plan.UnmappedColumns);
    }
}
