using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Data.Entities;
using Tennis.Api.Models;
using Tennis.Api.Services;

namespace Tennis.Api.UnitTests;

public class PlayerNavigationTests
{
    [Fact]
    public async Task Match_list_is_newest_played_on_first_and_pages_do_not_overlap()
    {
        using var archive = new Archive();
        var home = archive.AddPlayer("Home Player");
        var away = archive.AddPlayer("Away Player");
        var january = archive.AddMatch(home, away, new DateOnly(2022, 1, 10), 2022, "late-season-early-calendar", homeWins: true);
        var december = archive.AddMatch(home, away, new DateOnly(2021, 12, 20), 2021, "earlier-season-later-calendar", homeWins: false);
        var sameDayA = archive.AddMatch(home, away, new DateOnly(2021, 6, 1), 2021, "m-a", homeWins: true);
        var sameDayB = archive.AddMatch(home, away, new DateOnly(2021, 6, 1), 2021, "m-b", homeWins: true);
        var undated = archive.AddMatch(home, away, null, 2025, "undated-later-file", homeWins: true);
        await archive.Db.SaveChangesAsync();

        var service = new MatchService(archive.Db);
        var page1 = await service.GetPageAsync(1, 2, null, null, null, null, null, null, null, CancellationToken.None);
        var page2 = await service.GetPageAsync(2, 2, null, null, null, null, null, null, null, CancellationToken.None);
        var page3 = await service.GetPageAsync(3, 2, null, null, null, null, null, null, null, CancellationToken.None);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal([january.Id, december.Id], page1.Items.Select(match => match.Id).ToArray());
        Assert.Equal([sameDayA.Id, sameDayB.Id], page2.Items.Select(match => match.Id).ToArray());
        Assert.Equal([undated.Id], page3.Items.Select(match => match.Id).ToArray());
        Assert.DoesNotContain(page2.Items, match => page1.Items.Any(earlier => earlier.Id == match.Id));
    }

    [Fact]
    public async Task Player_history_returns_ten_newest_then_the_older_page()
    {
        using var archive = new Archive();
        var subject = archive.AddPlayer("History Subject");
        var rivals = Enumerable.Range(1, 12).Select(day => archive.AddPlayer("Rival " + day)).ToArray();
        var matches = new Match[12];
        for (var day = 1; day <= 12; day++)
        {
            var subjectWins = day == 12;
            var round = day == 12 ? "QF" : null;
            var homeScore = day == 12 ? "5" : day == 11 ? null : "6";
            var awayScore = day == 12 ? "7" : day == 11 ? null : "3";
            matches[day - 1] = archive.AddMatch(
                rivals[day - 1],
                subject,
                new DateOnly(2021, 3, day),
                2021,
                "history-" + day.ToString("00"),
                homeWins: !subjectWins,
                round: round,
                homeSet: homeScore,
                awaySet: awayScore,
                tournament: day == 12 ? "Australian Open" : "Lyon",
                surface: day == 12 ? "Hard" : "Clay");
        }

        var undatedRival = archive.AddPlayer("Undated Rival");
        var undated = archive.AddMatch(undatedRival, subject, null, 2026, "history-undated", homeWins: true);
        await archive.Db.SaveChangesAsync();

        var service = new PlayerService(archive.Db);
        var first = await service.GetMatchHistoryAsync(subject.Id, 1, PlayerService.DefaultHistoryPageSize, CancellationToken.None);
        var second = await service.GetMatchHistoryAsync(subject.Id, 2, PlayerService.DefaultHistoryPageSize, CancellationToken.None);
        var detail = await service.GetAsync(subject.Id, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(13, first.TotalCount);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(3, second.Items.Count);
        Assert.Equal(matches[11].Id, first.Items[0].Id);
        Assert.Equal(new DateOnly(2021, 3, 12), first.Items[0].PlayedOn);
        Assert.Equal(rivals[11].Id, first.Items[0].OpponentId);
        Assert.Equal("Rival 12", first.Items[0].OpponentName);
        Assert.Equal("Australian Open", first.Items[0].TournamentName);
        Assert.Equal("QF", first.Items[0].Round);
        Assert.Equal("Hard", first.Items[0].Surface);
        Assert.Equal("5-7", first.Items[0].Score);
        Assert.True(first.Items[0].IsWinner);
        Assert.Equal(matches[10].Id, first.Items[1].Id);
        Assert.Null(first.Items[1].Round);
        Assert.Null(first.Items[1].Score);
        Assert.False(first.Items[1].IsWinner);
        Assert.Equal(undated.Id, second.Items[^1].Id);
        Assert.Null(second.Items[^1].PlayedOn);
        Assert.Equal(first.Items.Select(item => item.Id), detail!.RecentMatches.Select(item => item.Id));
        Assert.Equal("5-7", detail.RecentMatches[0].Score);
        Assert.Equal(rivals[11].Id, detail.RecentMatches[0].OpponentId);
        Assert.DoesNotContain(second.Items, item => first.Items.Any(newer => newer.Id == item.Id));
    }

    [Fact]
    public async Task Player_name_filter_matches_part_of_the_name_without_other_players()
    {
        using var archive = new Archive();
        var alcaraz = archive.AddPlayer("Carlos Alcaraz");
        var sinner = archive.AddPlayer("Jannik Sinner");
        archive.AddPlayer("Alexander Zverev");
        await archive.Db.SaveChangesAsync();

        var players = new PlayerService(archive.Db);
        var byGiven = await players.GetPageAsync(1, 25, "carlos", CancellationToken.None);
        var byFamily = await players.GetPageAsync(1, 25, "ALCARAZ", CancellationToken.None);
        var byMiddle = await players.GetPageAsync(1, 25, "inn", CancellationToken.None);
        var none = await players.GetPageAsync(1, 25, "zzz", CancellationToken.None);
        var all = await players.GetPageAsync(1, 25, "   ", CancellationToken.None);

        Assert.Equal([alcaraz.Id], byGiven.Items.Select(player => player.Id).ToArray());
        Assert.Equal([alcaraz.Id], byFamily.Items.Select(player => player.Id).ToArray());
        Assert.Equal([sinner.Id], byMiddle.Items.Select(player => player.Id).ToArray());
        Assert.Empty(none.Items);
        Assert.Equal(0, none.TotalCount);
        Assert.Equal(3, all.TotalCount);

        var sinnerMatch = archive.AddMatch(sinner, alcaraz, new DateOnly(2021, 5, 2), 2021, "name-filter-match", homeWins: true);
        var other = archive.AddPlayer("Other Player");
        var rival = archive.AddPlayer("Other Rival");
        archive.AddMatch(other, rival, new DateOnly(2021, 8, 1), 2021, "someone-else", homeWins: false);
        await archive.Db.SaveChangesAsync();

        var matches = new MatchService(archive.Db);
        var filtered = await matches.GetPageAsync(1, 25, sinner.Id, null, null, null, null, null, null, CancellationToken.None);
        Assert.Equal([sinnerMatch.Id], filtered.Items.Select(match => match.Id).ToArray());
    }

    [Fact]
    public async Task Unknown_player_history_is_missing()
    {
        using var archive = new Archive();
        var service = new PlayerService(archive.Db);

        var history = await service.GetMatchHistoryAsync(Guid.NewGuid(), 1, 10, CancellationToken.None);

        Assert.Null(history);
    }

    [Fact]
    public void Default_history_page_size_is_ten_and_allowed()
    {
        Assert.Equal(10, PlayerService.DefaultHistoryPageSize);
        Assert.Null(PageQuery.Validate(1, PlayerService.DefaultHistoryPageSize));
    }

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
            DateOnly? playedOn,
            int year,
            string sourceKey,
            bool homeWins,
            string? round = null,
            string? homeSet = null,
            string? awaySet = null,
            string? tournament = null,
            string? surface = null)
        {
            TournamentEdition? edition = null;
            if (tournament is not null || surface is not null)
            {
                _sourceIds++;
                var owner = new Tournament
                {
                    Id = Guid.NewGuid(),
                    SourceId = "t" + _sourceIds,
                    Name = tournament ?? "Tournament"
                };
                edition = new TournamentEdition
                {
                    Id = Guid.NewGuid(),
                    Tournament = owner,
                    Year = year,
                    Surface = surface,
                    Name = owner.Name
                };
                Db.Add(owner);
                Db.Add(edition);
            }

            var document = new JsonObject();
            if (round is not null)
            {
                document["round"] = round;
            }

            if (homeSet is not null)
            {
                document["home_set_1_score"] = homeSet;
            }

            if (awaySet is not null)
            {
                document["away_set_1_score"] = awaySet;
            }

            var match = new Match
            {
                Id = Guid.NewGuid(),
                SourceKey = sourceKey,
                SourceYear = year,
                SeasonFileName = "2021.csv",
                WinnerCode = (short)(homeWins ? 1 : 2),
                PlayedOn = playedOn,
                RawRecord = document.ToJsonString(),
                TournamentEdition = edition,
                ImportFile = _file,
                Players =
                [
                    new MatchPlayer
                    {
                        Id = Guid.NewGuid(),
                        Player = home,
                        Side = "home",
                        IsWinner = homeWins,
                        Statistics = "{}",
                        Odds = "{}"
                    },
                    new MatchPlayer
                    {
                        Id = Guid.NewGuid(),
                        Player = away,
                        Side = "away",
                        IsWinner = !homeWins,
                        Statistics = "{}",
                        Odds = "{}"
                    }
                ]
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
