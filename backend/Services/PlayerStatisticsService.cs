using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Import;
using Tennis.Api.Statistics;

namespace Tennis.Api.Services;

// Loads stored matches and hands them to PlayerStatisticsCalculator. Odds methods can call
// Calculate directly, with an asOfDate and the match being priced left out of the history.
public class PlayerStatisticsService
{
    private readonly AppDbContext _db;

    public PlayerStatisticsService(AppDbContext db)
    {
        _db = db;
    }

    public PlayerStatisticsReport Calculate(IReadOnlyList<PlayerMatchFact> matches, PlayerStatisticsRequest request) =>
        PlayerStatisticsCalculator.Calculate(matches, request);

    public async Task<PlayerStatisticsReport?> GetAsync(Guid playerId, PlayerStatisticsRequest request, CancellationToken cancellationToken)
    {
        var exists = await _db.Players.AsNoTracking().AnyAsync(player => player.Id == playerId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var rows = await _db.MatchPlayers
            .AsNoTracking()
            .Where(row => row.PlayerId == playerId)
            .Select(row => new
            {
                row.MatchId,
                row.Match.SourceKey,
                row.Match.SourceYear,
                row.Match.PlayedOn,
                row.Match.RawRecord,
                Surface = row.Match.TournamentEdition != null ? row.Match.TournamentEdition.Surface : null,
                Level = row.Match.TournamentEdition != null ? row.Match.TournamentEdition.Level : null,
                TournamentName = row.Match.TournamentEdition != null ? row.Match.TournamentEdition.Tournament.Name : null,
                EditionName = row.Match.TournamentEdition != null ? row.Match.TournamentEdition.Name : null,
                row.IsWinner,
                OpponentRanking = row.Match.Players
                    .Where(other => other.Id != row.Id)
                    .OrderBy(other => other.Side)
                    .Select(other => other.Ranking)
                    .FirstOrDefault(),
                Aces = row.PlayerStatistics == null ? null : row.PlayerStatistics.Aces,
                DoubleFaults = row.PlayerStatistics == null ? null : row.PlayerStatistics.DoubleFaults,
                ServicePointsWonPercent = row.PlayerStatistics == null ? null : row.PlayerStatistics.ServicePointsWonPercent,
                ReturnPointsWonPercent = row.PlayerStatistics == null ? null : row.PlayerStatistics.ReturnPointsWonPercent,
                BreakPointsWonPercent = row.PlayerStatistics == null ? null : row.PlayerStatistics.BreakPointsWonPercent,
                BreakPointsSavedPercent = row.PlayerStatistics == null ? null : row.PlayerStatistics.BreakPointsSavedPercent,
                BreakPointsConverted = row.PlayerStatistics == null ? null : row.PlayerStatistics.BreakPointsConverted,
                BreakPointChances = row.PlayerStatistics == null ? null : row.PlayerStatistics.BreakPointChances,
                BreakPointsSaved = row.PlayerStatistics == null ? null : row.PlayerStatistics.BreakPointsSaved,
                BreakPointsFaced = row.PlayerStatistics == null ? null : row.PlayerStatistics.BreakPointsFaced,
                HasStatistics = row.PlayerStatistics != null
            })
            .ToListAsync(cancellationToken);

        var facts = rows
            .Select(row => new PlayerMatchFact(
                row.MatchId,
                row.SourceKey,
                row.SourceYear,
                row.PlayedOn,
                null,
                row.Surface,
                row.Level,
                string.IsNullOrWhiteSpace(row.TournamentName) ? row.EditionName : row.TournamentName,
                TennisDataRowMapper.ReadRawText(row.RawRecord, "status"),
                TennisDataRowMapper.ReadRawText(row.RawRecord, "status_extra"),
                row.IsWinner,
                row.OpponentRanking,
                row.HasStatistics
                    ? new PlayerMatchStatFact(
                        row.Aces,
                        row.DoubleFaults,
                        row.ServicePointsWonPercent,
                        row.ReturnPointsWonPercent,
                        row.BreakPointsWonPercent,
                        row.BreakPointsSavedPercent,
                        row.BreakPointsConverted,
                        row.BreakPointChances,
                        row.BreakPointsSaved,
                        row.BreakPointsFaced)
                    : null))
            .ToArray();
        return Calculate(facts, request);
    }
}
