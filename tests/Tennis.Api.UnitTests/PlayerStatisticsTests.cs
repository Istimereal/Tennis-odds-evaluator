using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Data.Entities;
using Tennis.Api.Services;
using Tennis.Api.Statistics;

namespace Tennis.Api.UnitTests;

public class PlayerStatisticsTests
{
    [Fact]
    public void Cutoff_keeps_only_matches_completed_strictly_before_the_date()
    {
        var earlier = Fact(1, win: true);
        var sameDay = Fact(2, win: true);
        var completedDayBefore = Fact(2, win: false, completed: new DateTimeOffset(2021, 6, 1, 22, 0, 0, TimeSpan.Zero));
        var completedOnCutoff = Fact(1, win: true, completed: new DateTimeOffset(2021, 6, 2, 1, 0, 0, TimeSpan.Zero));
        var undated = Fact(0, win: true);
        var asOf = new DateOnly(2021, 6, 2);
        var report = Calculate(
            [earlier, sameDay, completedDayBefore, completedOnCutoff, undated],
            request => request with { AsOfDate = asOf });

        Assert.Equal(2, report.Overall.Matches);
        Assert.Equal(1, report.Overall.Wins);
        Assert.Equal(1, report.Overall.Losses);
        Assert.Equal(50.0m, report.Overall.WinPercentage);
    }

    [Fact]
    public void Evaluated_match_is_left_out_of_its_own_history()
    {
        var priced = Fact(1, win: true, id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var previous = Fact(2, win: false);
        var report = Calculate([priced, previous], request => request with { ExcludeMatchId = priced.MatchId, AsOfDate = new DateOnly(2021, 6, 3) });

        Assert.Equal(1, report.Overall.Matches);
        Assert.Equal(0, report.Overall.Wins);
        Assert.Equal(1, report.Overall.Losses);
    }

    [Fact]
    public void Exhibition_cancelled_unfinished_and_invalid_matches_are_excluded()
    {
        var finished = Fact(1, win: true, status: "finished");
        var retired = Fact(2, win: true, status: "retired");
        var walkover = Fact(3, win: false, status: "walkover");
        var cancelled = Fact(4, win: true, status: "cancelled");
        var canceled = Fact(5, win: true, statusExtra: "canceled");
        var unfinished = Fact(6, win: false, status: "unfinished");
        var invalid = Fact(7, win: true, level: "invalid");
        var exhibition = Fact(8, win: true, tournament: "Delray Beach Exhibition", status: "finished");
        var undecided = Fact(9, win: null, status: "finished");
        var report = Calculate([finished, retired, walkover, cancelled, canceled, unfinished, invalid, exhibition, undecided]);

        Assert.Equal(3, report.Overall.Matches);
        Assert.Equal(2, report.Overall.Wins);
        Assert.Equal(1, report.Overall.Losses);
        Assert.Equal(5, report.Coverage.ExcludedMatches);
        Assert.Equal(1, report.Coverage.UndecidedResults);
    }

    [Fact]
    public void Ranking_groups_overlap_and_average_uses_the_stored_rank()
    {
        var ranks = new int?[] { 2, 4, 10, 15, 30, 55, 100, 200, 201, null };
        var matches = ranks.Select((rank, index) => Fact(index + 1, win: index % 2 == 0, rank: rank)).ToArray();
        var report = Calculate(matches);

        Assert.Equal(1, Group(report, "1-3").Matches);
        Assert.Equal(2, Group(report, "top 5").Matches);
        Assert.Equal(3, Group(report, "top 10").Matches);
        Assert.Equal(2, Group(report, "top 10 without top 3").Matches);
        Assert.Equal(1, Group(report, "11-20").Matches);
        Assert.Equal(1, Group(report, "21-40").Matches);
        Assert.Equal(1, Group(report, "41-70").Matches);
        Assert.Equal(1, Group(report, "71-120").Matches);
        Assert.Equal(1, Group(report, "121-200").Matches);
        Assert.Equal(9, report.OpponentRanking.SampleSize);
        Assert.Equal(1, report.OpponentRanking.Missing);
        Assert.Equal(68.6m, report.OpponentRanking.Average);
    }

    [Fact]
    public void Surface_filter_keeps_indoor_hard_separate_from_hard()
    {
        var matches = new[]
        {
            Fact(1, win: true, surface: "Hard"),
            Fact(2, win: false, surface: "Indoor Hard"),
            Fact(3, win: true, surface: "Clay"),
            Fact(4, win: true, surface: "Grass"),
            Fact(5, win: false, surface: "Carpet")
        };
        var all = Calculate(matches);
        var selected = Calculate(matches, request => request with { Surfaces = ["hard", "clay"] });

        Assert.Equal(1, Surface(all, "hard").Matches);
        Assert.Equal(1, Surface(all, "indoor hard").Losses);
        Assert.Equal(1, Surface(all, "clay").Wins);
        Assert.Equal(1, Surface(all, "grass").Wins);
        Assert.Equal(1, all.Coverage.MissingOrOtherSurface);
        Assert.Equal(2, selected.Overall.Matches);
        Assert.Equal(1, Surface(selected, "hard").Matches);
        Assert.Equal(1, Surface(selected, "clay").Matches);
        Assert.Equal(0, Surface(selected, "indoor hard").Matches);
        Assert.Equal(0, Surface(selected, "grass").Matches);
    }

    [Fact]
    public void Challenger_is_not_counted_as_atp_tour()
    {
        var matches = new[]
        {
            Fact(1, win: true, level: "ATP 250"),
            Fact(2, win: false, level: "ATP Challenger 125"),
            Fact(3, win: true, level: "Grand Slam")
        };
        var all = Calculate(matches);
        var challengers = Calculate(matches, request => request with { Level = PlayerStatisticsTerms.Challenger });

        Assert.Equal(3, Level(all, "All").Matches);
        Assert.Equal(1, Level(all, "ATP Tour").Wins);
        Assert.Equal(1, Level(all, "Challenger").Losses);
        Assert.Equal(1, challengers.Overall.Matches);
        Assert.Equal(0, Level(challengers, "ATP Tour").Matches);
    }

    [Fact]
    public void Recent_form_uses_the_newest_matches_and_the_season_year()
    {
        var matches = Enumerable.Range(1, 6)
            .Select(day => Fact(day, win: day % 2 == 0, year: 2021))
            .Append(Fact(1, win: true, year: 2020, month: 12))
            .ToArray();
        var report = Calculate(matches);
        var limited = Calculate(matches, request => request with { RecentMatchCount = 3 });

        Assert.Equal(4, report.Overall.Wins);
        Assert.Equal(3, report.Overall.Losses);
        Assert.Equal(57.1m, report.Overall.WinPercentage);
        Assert.Equal(3, Form(report, "last-5").Wins);
        Assert.Equal(2, Form(report, "last-5").Losses);
        Assert.Equal(60.0m, Form(report, "last-5").WinPercentage);
        Assert.Equal(7, Form(report, "last-10").Matches);
        Assert.Equal(7, Form(report, "last-20").Matches);
        Assert.Equal(2021, report.Filters.SeasonYear);
        Assert.Equal(6, Form(report, "season").Matches);
        Assert.Equal(3, limited.Overall.Matches);
        Assert.Equal(3, Form(limited, "last-5").Matches);
    }

    [Fact]
    public void Date_range_is_inclusive_and_still_stops_at_the_cutoff()
    {
        var matches = new[]
        {
            Fact(1, win: true),
            Fact(2, win: false),
            Fact(3, win: true),
            Fact(0, win: true)
        };
        var ranged = Calculate(matches, request => request with { From = new DateOnly(2021, 6, 2), To = new DateOnly(2021, 6, 3) });
        var cutoff = Calculate(matches, request => request with
        {
            From = new DateOnly(2021, 6, 1),
            To = new DateOnly(2021, 6, 3),
            AsOfDate = new DateOnly(2021, 6, 3)
        });

        Assert.Equal(2, ranged.Overall.Matches);
        Assert.Equal(1, ranged.Overall.Wins);
        Assert.Equal(2, cutoff.Overall.Matches);
        Assert.Equal(1, cutoff.Overall.Wins);
        Assert.Equal(1, cutoff.Overall.Losses);
    }

    [Fact]
    public void Missing_counts_stay_missing_and_rates_use_counts_before_percentages()
    {
        var matches = new[]
        {
            Fact(1, stats: Stats(aces: 4, doubleFaults: 1, service: 80, @return: 40, converted: 1, chances: 2, bpWon: 50, saved: 1, faced: 2, bpSaved: 50)),
            Fact(2, stats: Stats(aces: 0, doubleFaults: 3, service: 40, @return: 20, converted: 1, chances: 8, bpWon: 12.5m, saved: 1, faced: 8, bpSaved: 12.5m)),
            Fact(3, stats: Stats(service: 60))
        };
        var report = Calculate(matches);
        var aces = Line(report, "aces");
        var faults = Line(report, "double-faults");
        var service = Line(report, "service-points-won");
        var @return = Line(report, "return-points-won");
        var won = Line(report, "break-points-won");
        var saved = Line(report, "break-points-saved");

        Assert.Equal(4, aces.Total);
        Assert.Equal(2, aces.SampleSize);
        Assert.Equal(2.0m, aces.PerMatch);
        Assert.Equal(StatBasis.StoredCounts, aces.Basis);
        Assert.Equal(4, faults.Total);
        Assert.Equal(2, faults.SampleSize);
        Assert.Equal(60.0m, service.Value);
        Assert.Equal(StatBasis.AverageOfPercentages, service.Basis);
        Assert.Equal(3, service.SampleSize);
        Assert.Null(service.Numerator);
        Assert.Equal(30.0m, @return.Value);
        Assert.Equal(2, @return.SampleSize);
        Assert.Equal(20.0m, won.Value);
        Assert.Equal(StatBasis.PooledCounts, won.Basis);
        Assert.Equal(2, won.Numerator);
        Assert.Equal(10, won.Denominator);
        Assert.Equal(2, won.SampleSize);
        Assert.Equal(20.0m, saved.Value);
        Assert.Equal(2, saved.Numerator);
        Assert.Equal(10, saved.Denominator);
        Assert.Equal(1, report.Coverage.MissingAces);
        Assert.Equal(1, report.Coverage.MissingDoubleFaults);
        Assert.Equal(0, report.Coverage.MissingServicePointsWon);
        Assert.Equal(1, report.Coverage.MissingReturnPointsWon);
    }

    [Fact]
    public void Percentage_only_break_points_are_an_average_with_a_sample_size()
    {
        var report = Calculate(
        [
            Fact(1, stats: Stats(bpWon: 50, bpSaved: 0)),
            Fact(2, stats: Stats(bpWon: 12.5m, bpSaved: 100))
        ]);
        var won = Line(report, "break-points-won");
        var saved = Line(report, "break-points-saved");

        Assert.Equal(31.3m, won.Value);
        Assert.Equal(StatBasis.AverageOfPercentages, won.Basis);
        Assert.Equal(2, won.SampleSize);
        Assert.Null(won.Numerator);
        Assert.Null(won.Denominator);
        Assert.Equal(50.0m, saved.Value);
        Assert.Equal(StatBasis.AverageOfPercentages, saved.Basis);
    }

    [Fact]
    public void Count_samples_are_not_mixed_with_percentage_only_matches()
    {
        var report = Calculate(
        [
            Fact(1, stats: Stats(converted: 1, chances: 2, bpWon: 50)),
            Fact(2, stats: Stats(bpWon: 12.5m))
        ]);
        var won = Line(report, "break-points-won");

        Assert.Equal(50.0m, won.Value);
        Assert.Equal(StatBasis.PooledCounts, won.Basis);
        Assert.Equal(1, won.SampleSize);
        Assert.Equal(1, won.Numerator);
        Assert.Equal(2, won.Denominator);
        Assert.Contains("kept out", won.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Empty_history_has_no_invented_percentage()
    {
        var report = Calculate([]);

        Assert.Equal(0, report.Overall.Matches);
        Assert.Null(report.Overall.WinPercentage);
        Assert.Null(Line(report, "aces").Value);
        Assert.Equal(StatBasis.None, Line(report, "aces").Basis);
        Assert.Equal(0, Line(report, "service-points-won").SampleSize);
        Assert.Null(report.OpponentRanking.Average);
    }

    [Fact]
    public void Filters_reject_unknown_values_and_accept_two_surfaces()
    {
        Assert.False(PlayerStatisticsRequest.TryCreate(null, ["hard", "clay", "grass"], null, null, null, null, null, null, out _, out var tooMany));
        Assert.Equal("Select one or two surfaces.", tooMany);
        Assert.False(PlayerStatisticsRequest.TryCreate(null, ["carpet"], null, null, null, null, null, null, out _, out var surface));
        Assert.Contains("Surface", surface, StringComparison.Ordinal);
        Assert.False(PlayerStatisticsRequest.TryCreate(null, null, "itf", null, null, null, null, null, out _, out var level));
        Assert.Contains("Level", level, StringComparison.Ordinal);
        Assert.False(PlayerStatisticsRequest.TryCreate(null, null, null, null, null, "top 4", null, null, out _, out var group));
        Assert.Contains("Ranking group", group, StringComparison.Ordinal);
        Assert.False(PlayerStatisticsRequest.TryCreate(null, null, null, null, null, null, 0, null, out _, out var recent));
        Assert.Contains("Recent", recent, StringComparison.Ordinal);
        Assert.False(PlayerStatisticsRequest.TryCreate(null, null, null, new DateOnly(2021, 6, 2), new DateOnly(2021, 6, 1), null, null, null, out _, out var range));
        Assert.Contains("date range", range, StringComparison.Ordinal);

        var created = PlayerStatisticsRequest.TryCreate(
            new DateOnly(2021, 6, 2),
            ["Indoor-Hard", "clay", "clay"],
            "atp",
            null,
            null,
            "Top 10 without top 3",
            5,
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            out var request,
            out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.Equal(["indoor hard", "clay"], request!.Surfaces);
        Assert.Equal(PlayerStatisticsTerms.AtpTour, request.Level);
        Assert.Equal("top 10 without top 3", request.RankingGroup);
        Assert.Equal(5, request.RecentMatchCount);
    }

    [Fact]
    public async Task Service_reads_opponent_ranking_and_leaves_ranking_points_alone()
    {
        using var archive = new Archive();
        var subject = archive.AddPlayer("Subject");
        var higher = archive.AddPlayer("Higher");
        var unranked = archive.AddPlayer("Unranked");
        var counted = archive.AddMatch(
            subject,
            higher,
            new DateOnly(2021, 5, 1),
            "counted",
            homeWins: true,
            homeRanking: 12,
            awayRanking: 40,
            rawPoints: ("800", "900"),
            level: "ATP 250",
            surface: "Hard",
            aces: 7,
            service: 80);
        archive.AddMatch(
            unranked,
            subject,
            new DateOnly(2021, 5, 2),
            "unranked",
            homeWins: true,
            homeRanking: null,
            awayRanking: 12,
            rawPoints: ("1500", "10"),
            level: "ATP Challenger 90",
            surface: "Clay");
        archive.AddMatch(
            subject,
            higher,
            new DateOnly(2021, 5, 3),
            "cutoff",
            homeWins: true,
            homeRanking: 12,
            awayRanking: 1,
            rawPoints: ("1", "1"),
            level: "ATP 250",
            surface: "Hard");
        archive.AddMatch(
            subject,
            higher,
            new DateOnly(2021, 4, 1),
            "exhibition",
            homeWins: true,
            homeRanking: 12,
            awayRanking: 8,
            rawPoints: ("8", "8"),
            tournament: "Delray Beach Exhibition",
            status: "finished");
        await archive.Db.SaveChangesAsync();

        var service = new PlayerStatisticsService(archive.Db);
        var report = await service.GetAsync(
            subject.Id,
            new PlayerStatisticsRequest(new DateOnly(2021, 5, 3), [], null, null, null, null, null, null),
            CancellationToken.None);
        var withoutItself = await service.GetAsync(
            subject.Id,
            new PlayerStatisticsRequest(new DateOnly(2021, 5, 3), [], null, null, null, null, null, counted.Id),
            CancellationToken.None);
        var missing = await service.GetAsync(Guid.NewGuid(), PlayerStatisticsRequest.Unfiltered, CancellationToken.None);

        Assert.Null(missing);
        Assert.NotNull(report);
        Assert.Equal(2, report.Overall.Matches);
        Assert.Equal(1, report.Overall.Wins);
        Assert.Equal(1, report.Overall.Losses);
        Assert.Equal(40.0m, report.OpponentRanking.Average);
        Assert.Equal(1, report.OpponentRanking.SampleSize);
        Assert.Equal(1, report.OpponentRanking.Missing);
        Assert.Equal(1, Group(report, "21-40").Matches);
        Assert.Equal(0, Group(report, "1-3").Matches);
        Assert.Equal(1, report.Coverage.ExcludedMatches);
        Assert.Equal(7, Line(report, "aces").Total);
        Assert.Equal(1, Line(report, "aces").SampleSize);
        Assert.Equal(80.0m, Line(report, "service-points-won").Value);
        Assert.NotNull(withoutItself);
        Assert.Equal(1, withoutItself.Overall.Losses);
        Assert.Equal(0, withoutItself.Overall.Wins);
        Assert.Null(withoutItself.OpponentRanking.Average);
        Assert.Equal(0, Line(withoutItself, "aces").SampleSize);
    }

    private static PlayerStatisticsReport Calculate(
        IReadOnlyList<PlayerMatchFact> matches,
        Func<PlayerStatisticsRequest, PlayerStatisticsRequest>? adjust = null)
    {
        var request = adjust is null ? PlayerStatisticsRequest.Unfiltered : adjust(PlayerStatisticsRequest.Unfiltered);
        return PlayerStatisticsCalculator.Calculate(matches, request);
    }

    private static ResultsSummary Group(PlayerStatisticsReport report, string group) =>
        report.RankingGroups.Single(row => row.Group == group).Results;

    private static ResultsSummary Surface(PlayerStatisticsReport report, string surface) =>
        report.Surfaces.Single(row => row.Surface == surface).Results;

    private static ResultsSummary Level(PlayerStatisticsReport report, string level) =>
        report.Levels.Single(row => row.Level == level).Results;

    private static ResultsSummary Form(PlayerStatisticsReport report, string key) =>
        report.RecentForm.Single(row => row.Key == key).Results;

    private static StatLine Line(PlayerStatisticsReport report, string key) =>
        report.Statistics.Single(row => row.Key == key);

    private static PlayerMatchFact Fact(
        int day,
        bool? win = true,
        int? rank = 50,
        string? surface = "hard",
        string? level = "ATP Tour",
        string? status = "finished",
        string? statusExtra = null,
        string? tournament = "Tournament",
        PlayerMatchStatFact? stats = null,
        Guid? id = null,
        int month = 6,
        int year = 2021,
        DateTimeOffset? completed = null,
        int? sourceYear = null)
    {
        var matchId = id ?? Guid.NewGuid();
        return new PlayerMatchFact(
            matchId,
            matchId.ToString("N"),
            sourceYear ?? year,
            day == 0 ? null : new DateOnly(year, month, day),
            completed,
            surface,
            level,
            tournament,
            status,
            statusExtra,
            win,
            rank,
            stats);
    }

    private static PlayerMatchStatFact Stats(
        int? aces = null,
        int? doubleFaults = null,
        decimal? service = null,
        decimal? @return = null,
        decimal? bpWon = null,
        decimal? bpSaved = null,
        int? converted = null,
        int? chances = null,
        int? saved = null,
        int? faced = null) =>
        new(aces, doubleFaults, service, @return, bpWon, bpSaved, converted, chances, saved, faced);

    private sealed class Archive : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ImportFileRecord _file;
        private int _sourceIds;

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
            _file = new ImportFileRecord
            {
                Id = Guid.NewGuid(),
                ImportBatch = batch,
                OriginalFileName = "2021.csv",
                Sha256 = "test",
                Provenance = "test",
                HeaderJson = "[]",
                UnmappedColumns = "[]",
                Status = "imported",
                ImportedAt = DateTimeOffset.UnixEpoch
            };
            Db.Add(batch);
            Db.Add(_file);
        }

        public AppDbContext Db { get; }

        public Player AddPlayer(string name)
        {
            _sourceIds++;
            var player = new Player
            {
                Id = Guid.NewGuid(),
                SourceId = "p" + _sourceIds,
                Name = name
            };
            Db.Add(player);
            return player;
        }

        public Match AddMatch(
            Player home,
            Player away,
            DateOnly playedOn,
            string sourceKey,
            bool homeWins,
            int? homeRanking,
            int? awayRanking,
            (string Home, string Away) rawPoints,
            string? level = null,
            string? surface = null,
            string? tournament = "Tournament",
            string status = "finished",
            int? aces = null,
            decimal? service = null)
        {
            _sourceIds++;
            var owner = new Tournament
            {
                Id = Guid.NewGuid(),
                SourceId = "t" + _sourceIds,
                Name = tournament ?? "Tournament"
            };
            var edition = new TournamentEdition
            {
                Id = Guid.NewGuid(),
                Tournament = owner,
                Year = playedOn.Year,
                Surface = surface,
                Level = level,
                Name = owner.Name
            };
            var document = new JsonObject
            {
                ["status"] = status,
                ["home_points"] = rawPoints.Home,
                ["away_points"] = rawPoints.Away
            };
            var homeSide = new MatchPlayer
            {
                Id = Guid.NewGuid(),
                Player = home,
                Side = "home",
                IsWinner = homeWins,
                Ranking = homeRanking,
                Statistics = "{}",
                Odds = "{}"
            };
            var awaySide = new MatchPlayer
            {
                Id = Guid.NewGuid(),
                Player = away,
                Side = "away",
                IsWinner = !homeWins,
                Ranking = awayRanking,
                Statistics = "{}",
                Odds = "{}"
            };
            if (aces is not null || service is not null)
            {
                homeSide.PlayerStatistics = new MatchPlayerStatistics
                {
                    Id = Guid.NewGuid(),
                    Aces = aces,
                    ServicePointsWonPercent = service
                };
            }

            var match = new Match
            {
                Id = Guid.NewGuid(),
                SourceKey = sourceKey,
                SourceYear = playedOn.Year,
                SeasonFileName = "2021.csv",
                WinnerCode = (short)(homeWins ? 1 : 2),
                PlayedOn = playedOn,
                RawRecord = document.ToJsonString(),
                TournamentEdition = edition,
                ImportFile = _file,
                Players = [homeSide, awaySide]
            };
            Db.Add(owner);
            Db.Add(edition);
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
