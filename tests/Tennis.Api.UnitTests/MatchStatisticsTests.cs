using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Data.Entities;
using Tennis.Api.Import;
using Tennis.Api.Odds;

namespace Tennis.Api.UnitTests;

public class MatchStatisticsTests
{
    private static readonly string[] Header =
    [
        "winner_code", "home_rank", "away_rank", "home_points", "away_points",
        "home_set_1_score", "away_set_1_score", "home_set_2_score", "away_set_2_score",
        "home_set_1_tiebreak", "away_set_1_tiebreak",
        "home_aces", "away_aces", "home_double_faults", "away_double_faults",
        "home_service_points_won_perc", "away_service_points_won_perc",
        "home_return_points_won_perc", "away_return_points_won_perc",
        "home_break_points_converted_perc", "away_break_points_won_perc",
        "home_break_points_saved_perc", "away_break_points_saved_perc",
        "home_first_serve_perc", "away_first_serve_perc"
    ];

    [Fact]
    public void Percentages_stay_percentage_points_and_ranking_points_stay_in_the_raw_record()
    {
        var plan = SeasonHeaderPlan.Classify(Header);
        var row = new[]
        {
            "1", "12", "120", "", "800",
            "7", "6", "6", "3",
            "8", "6",
            "5", "0", "1", "",
            "80%", "62.5",
            "40", "0.8",
            "50", "33",
            "70", "100",
            "61", "58"
        };

        var mapped = TennisDataRowMapper.Map(2021, plan, Header, row);

        Assert.Equal(5, mapped.Home.Parsed.Aces);
        Assert.Equal(0, mapped.Away.Parsed.Aces);
        Assert.Equal(1, mapped.Home.Parsed.DoubleFaults);
        Assert.Null(mapped.Away.Parsed.DoubleFaults);
        Assert.Equal(80m, mapped.Home.Parsed.ServicePointsWonPercent);
        Assert.Equal(62.5m, mapped.Away.Parsed.ServicePointsWonPercent);
        Assert.Equal(40m, mapped.Home.Parsed.ReturnPointsWonPercent);
        Assert.Equal(0.8m, mapped.Away.Parsed.ReturnPointsWonPercent);
        Assert.Equal(50m, mapped.Home.Parsed.BreakPointsWonPercent);
        Assert.Equal(33m, mapped.Away.Parsed.BreakPointsWonPercent);
        Assert.Equal(70m, mapped.Home.Parsed.BreakPointsSavedPercent);
        Assert.Contains("\"aces\":\"5\"", mapped.Home.StatisticsJson);
        Assert.Contains("\"first_serve_perc\":\"61\"", mapped.Home.StatisticsJson);
        using var homeStats = JsonDocument.Parse(mapped.Home.StatisticsJson);
        Assert.False(homeStats.RootElement.TryGetProperty("points", out _));
        Assert.False(homeStats.RootElement.TryGetProperty("set_1_score", out _));
        using var raw = JsonDocument.Parse(mapped.RawJson);
        Assert.Equal("800", raw.RootElement.GetProperty("away_points").GetString());
        Assert.Null(TennisMatchStatistics.ReadRankingPoints(mapped.RawJson, "home"));
        Assert.Equal(800, TennisMatchStatistics.ReadRankingPoints(mapped.RawJson, "away"));
        Assert.Equal(
            [
                new ParsedSetScore(1, 7, 6, 8, 6),
                new ParsedSetScore(2, 6, 3, null, null)
            ],
            mapped.Sets);
        Assert.DoesNotContain(plan.HomeIndexes, index => Header[index] is "home_points" or "home_set_1_score" or "home_set_1_tiebreak");
    }

    [Fact]
    public void Break_points_won_column_wins_when_both_percentage_columns_are_present()
    {
        var header = new[] { "winner_code", "home_break_points_won_perc", "home_break_points_converted_perc" };
        var mapped = TennisDataRowMapper.Map(2021, SeasonHeaderPlan.Classify(header), header, ["2", "25", "90"]);

        Assert.Equal(25m, mapped.Home.Parsed.BreakPointsWonPercent);
    }

    [Fact]
    public void Blank_cells_stay_null_and_a_stored_zero_stays_zero()
    {
        Assert.Null(TennisMatchStatistics.ParseCount(""));
        Assert.Null(TennisMatchStatistics.ParseCount(" "));
        Assert.Equal(0, TennisMatchStatistics.ParseCount("0"));
        Assert.Null(TennisMatchStatistics.ParsePercent(null));
        Assert.Equal(0m, TennisMatchStatistics.ParsePercent("0%"));
        Assert.Null(TennisMatchStatistics.ParsePercent("80."));
        Assert.Empty(TennisMatchStatistics.ParseSets(["home_set_3_score", "away_set_3_score"], ["", ""]));
    }

    [Fact]
    public void Reprocessing_clears_reserved_statistics_and_updates_sets_without_deleting_the_match()
    {
        var player = new MatchPlayer
        {
            Id = Guid.NewGuid(),
            Side = "home",
            PlayerStatistics = new MatchPlayerStatistics
            {
                Id = Guid.NewGuid(),
                FirstServePercent = 61m,
                TotalPointsWon = 90,
                Winners = 20
            }
        };
        player.PlayerStatistics.MatchPlayerId = player.Id;
        TennisMatchStatistics.ApplyParsed(player, new ParsedPlayerStatistics(4, 2, 55m, null, null, 100m));

        Assert.Equal(4, player.PlayerStatistics.Aces);
        Assert.Equal(55m, player.PlayerStatistics.ServicePointsWonPercent);
        Assert.Equal(100m, player.PlayerStatistics.BreakPointsSavedPercent);
        Assert.Null(player.PlayerStatistics.FirstServePercent);
        Assert.Null(player.PlayerStatistics.FirstServePointsWonPercent);
        Assert.Null(player.PlayerStatistics.SecondServePointsWonPercent);
        Assert.Null(player.PlayerStatistics.BreakPointsFaced);
        Assert.Null(player.PlayerStatistics.BreakPointsSaved);
        Assert.Null(player.PlayerStatistics.BreakPointChances);
        Assert.Null(player.PlayerStatistics.BreakPointsConverted);
        Assert.Null(player.PlayerStatistics.Winners);
        Assert.Null(player.PlayerStatistics.UnforcedErrors);
        Assert.Null(player.PlayerStatistics.NetPointsWon);
        Assert.Null(player.PlayerStatistics.TotalPointsWon);

        using var archive = new Archive();
        var match = archive.AddMatch();
        var side = new MatchPlayer
        {
            Id = Guid.NewGuid(),
            Side = "home",
            Statistics = "{}",
            Odds = "{}"
        };
        match.Players.Add(side);
        var removed = new List<MatchSet>();
        TennisMatchStatistics.ApplySets(
            match,
            [new ParsedSetScore(1, 6, 4, null, null), new ParsedSetScore(2, 3, 6, null, null)],
            removed);
        archive.Db.SaveChanges();
        var setOneId = match.Sets.Single(set => set.SetNumber == 1).Id;

        removed.Clear();
        var knownSets = match.Sets.Select(set => set.Id).ToHashSet();
        TennisMatchStatistics.ApplySets(
            match,
            [new ParsedSetScore(1, 7, 6, 7, 5), new ParsedSetScore(3, 6, 2, null, null)],
            removed);
        foreach (var set in match.Sets)
        {
            if (!knownSets.Contains(set.Id))
            {
                var entry = archive.Db.Entry(set);
                if (entry.State == EntityState.Detached)
                {
                    entry.State = EntityState.Added;
                }
            }
        }

        archive.Db.RemoveRange(removed);
        TennisMatchStatistics.ApplyParsed(side, new ParsedPlayerStatistics(9, null, 80m, null, null, null));
        var statisticsEntry = archive.Db.Entry(side.PlayerStatistics!);
        if (statisticsEntry.State == EntityState.Detached)
        {
            statisticsEntry.State = EntityState.Added;
        }

        archive.Db.SaveChanges();
        var statisticsId = side.PlayerStatistics!.Id;
        TennisMatchStatistics.ApplyParsed(side, new ParsedPlayerStatistics(3, 1, 70m, null, null, null));
        archive.Db.SaveChanges();
        Assert.Equal(statisticsId, side.PlayerStatistics.Id);
        Assert.Equal(3, side.PlayerStatistics.Aces);
        Assert.Equal(1, side.PlayerStatistics.DoubleFaults);
        Assert.Null(side.PlayerStatistics.TotalPointsWon);

        var sets = archive.Db.MatchSets.Where(set => set.MatchId == match.Id).OrderBy(set => set.SetNumber).ToList();
        Assert.Equal([1, 3], sets.Select(set => set.SetNumber).ToArray());
        Assert.Equal(setOneId, sets[0].Id);
        Assert.Equal(7, sets[0].HomeGames);
        Assert.Equal(6, sets[0].AwayGames);
        Assert.Equal(7, sets[0].HomeTiebreak);
        Assert.Equal(5, sets[0].AwayTiebreak);
        Assert.Equal(6, sets[1].HomeGames);
        Assert.Equal([2], removed.Select(set => set.SetNumber).ToArray());
        Assert.NotNull(archive.Db.Matches.Single(item => item.Id == match.Id));
        Assert.Equal(2, archive.Db.MatchSets.Count(set => set.MatchId == match.Id));
    }

    [Fact]
    public void Games_and_points_are_a_design_and_odds_input_has_no_match_statistics()
    {
        Assert.Equal("regular", MatchGameTypes.Regular);
        Assert.Equal("tiebreak", MatchGameTypes.Tiebreak);
        Assert.Equal("match-tiebreak", MatchGameTypes.MatchTiebreak);
        Assert.Equal("first", ServeStatuses.First);
        Assert.Equal("second", ServeStatuses.Second);
        Assert.Equal(MatchGameTypes.Regular, new MatchGameDesign(1, 1, "home", "home", MatchGameTypes.Regular).GameType);
        Assert.Equal(ServeStatuses.Second, new MatchPointDesign(1, "away", "home", "away", ServeStatuses.Second, "ace", "30-15").ServeStatus);
        Assert.Null(new MatchConditionsDesign(null, null, null, null, null, null).Venue);

        using var archive = new Archive();
        var names = archive.Db.Model.GetEntityTypes().Select(entity => entity.ClrType.Name).ToHashSet();
        Assert.Contains("MatchSet", names);
        Assert.Contains("MatchPlayerStatistics", names);
        Assert.DoesNotContain("MatchGameDesign", names);
        Assert.DoesNotContain("MatchPointDesign", names);

        var oddsFields = typeof(SourceMatch).GetProperties().Select(property => property.Name).ToArray();
        Assert.Contains("Score", oddsFields);
        Assert.DoesNotContain(oddsFields, name => name.Contains("Statistic", StringComparison.Ordinal) || name.Contains("Ace", StringComparison.Ordinal));
        Assert.Contains(TennisDataBinding.Unpublished, field => field.Field == "MatchPlayer.RankingPoints");
        Assert.Contains(TennisDataBinding.Unpublished, field => field.Field == "MatchGame");
        Assert.Contains(TennisDataBinding.Unpublished, field => field.Field == "MatchPoint");
    }

    private sealed class Archive : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Archive()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            Db = new AppDbContext(options);
            Db.Database.EnsureCreated();
            var batch = new ImportBatch
            {
                Id = Guid.NewGuid(),
                Mode = "test",
                Provenance = "test",
                StartedAt = DateTimeOffset.UnixEpoch
            };
            File = new ImportFileRecord
            {
                Id = Guid.NewGuid(),
                ImportBatch = batch,
                OriginalFileName = "2021-atp-season.csv",
                Sha256 = "test",
                Provenance = "test",
                HeaderJson = "[]",
                UnmappedColumns = "[]",
                Status = "imported",
                ImportedAt = DateTimeOffset.UnixEpoch
            };
            Db.Add(batch);
            Db.Add(File);
        }

        public AppDbContext Db { get; }
        public ImportFileRecord File { get; }

        public Match AddMatch()
        {
            var match = new Match
            {
                Id = Guid.NewGuid(),
                SourceKey = "match-stats-" + Guid.NewGuid().ToString("N"),
                SourceYear = 2021,
                SeasonFileName = "2021-atp-season.csv",
                WinnerCode = 1,
                RawRecord = "{}",
                ImportFile = File
            };
            Db.Add(match);
            return match;
        }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }
}
