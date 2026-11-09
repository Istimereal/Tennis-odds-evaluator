using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Data.Entities;
using Tennis.Api.Import;
using Tennis.Api.Models;

namespace Tennis.Api.Services;

public class PlayerService
{
    public const int DefaultHistoryPageSize = 10;

    private readonly AppDbContext _db;

    public PlayerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PageResponse<PlayerResponse>> GetPageAsync(int page, int pageSize, string? name, CancellationToken cancellationToken)
    {
        var query = _db.Players.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(name))
        {
            var term = name.Trim().ToLowerInvariant();
            query = query.Where(player => player.Name.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(player => player.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(player => new
            {
                player.Id,
                player.SourceId,
                player.Name,
                MatchCount = player.MatchPlayers.Count(),
                Wins = player.MatchPlayers.Count(row => row.IsWinner == true),
                Losses = player.MatchPlayers.Count(row => row.IsWinner == false)
            })
            .ToListAsync(cancellationToken);
        var items = rows
            .Select(row => new PlayerResponse(row.Id, row.SourceId, row.Name, null, row.MatchCount, row.Wins, row.Losses))
            .ToArray();
        return new PageResponse<PlayerResponse>(items, page, pageSize, total);
    }

    public async Task<PlayerDetailResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var player = await _db.Players
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.SourceId,
                item.Name,
                MatchCount = item.MatchPlayers.Count(),
                Wins = item.MatchPlayers.Count(row => row.IsWinner == true),
                Losses = item.MatchPlayers.Count(row => row.IsWinner == false)
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (player is null)
        {
            return null;
        }

        var sides = await _db.MatchPlayers
            .AsNoTracking()
            .Where(row => row.PlayerId == id)
            .Select(row => new
            {
                Surface = row.Match.TournamentEdition != null ? row.Match.TournamentEdition.Surface : null,
                row.IsWinner
            })
            .ToListAsync(cancellationToken);
        var surfaces = sides
            .GroupBy(row => string.IsNullOrWhiteSpace(row.Surface) ? "unmapped" : row.Surface)
            .Select(group => new SurfaceRecord(
                group.Key!,
                group.Count(row => row.IsWinner == true),
                group.Count(row => row.IsWinner == false)))
            .OrderBy(record => record.Surface, StringComparer.Ordinal)
            .ToArray();

        var recentRows = await LoadHistoryPageAsync(id, 1, DefaultHistoryPageSize, cancellationToken);
        var recent = recentRows
            .Select(row => new RecentMatchResponse(
                row.Id,
                row.SourceYear,
                row.PlayedOn,
                row.TournamentStart,
                row.WinnerCode,
                row.IsWinner,
                row.TournamentName,
                row.Surface,
                row.Level,
                row.Score,
                row.OpponentId,
                row.OpponentName,
                row.Round))
            .ToArray();

        return new PlayerDetailResponse(
            player.Id,
            player.SourceId,
            player.Name,
            null,
            player.MatchCount,
            player.Wins,
            player.Losses,
            surfaces,
            recent);
    }

    public async Task<PageResponse<PlayerMatchHistoryItem>?> GetMatchHistoryAsync(
        Guid playerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var exists = await _db.Players.AsNoTracking().AnyAsync(player => player.Id == playerId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var total = await HistorySides(playerId).CountAsync(cancellationToken);
        var rows = await LoadHistoryPageAsync(playerId, page, pageSize, cancellationToken);
        var items = rows
            .Select(row => new PlayerMatchHistoryItem(
                row.Id,
                row.PlayedOn,
                row.OpponentId,
                row.OpponentName,
                row.TournamentName,
                row.Round,
                row.Surface,
                row.Score,
                row.IsWinner))
            .ToArray();
        return new PageResponse<PlayerMatchHistoryItem>(items, page, pageSize, total);
    }

    private IQueryable<MatchPlayer> HistorySides(Guid playerId) =>
        _db.MatchPlayers.AsNoTracking().Where(row => row.PlayerId == playerId);

    private async Task<IReadOnlyList<HistoryRow>> LoadHistoryPageAsync(
        Guid playerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // HasValue first keeps undated matches after dated ones. DESC alone sorts null PlayedOn first on PostgreSQL.
        var pageRows = await HistorySides(playerId)
            .OrderByDescending(row => row.Match.PlayedOn.HasValue)
            .ThenByDescending(row => row.Match.PlayedOn)
            .ThenByDescending(row => row.Match.SourceYear)
            .ThenBy(row => row.Match.SourceKey)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new { row.MatchId, row.IsWinner })
            .ToListAsync(cancellationToken);
        if (pageRows.Count == 0)
        {
            return [];
        }

        var ids = pageRows.Select(row => row.MatchId).ToArray();
        var matches = await _db.Matches
            .AsNoTracking()
            .Include(match => match.TournamentEdition)
            .ThenInclude(edition => edition!.Tournament)
            .Include(match => match.Players)
            .ThenInclude(row => row.Player)
            .Where(match => ids.Contains(match.Id))
            .ToListAsync(cancellationToken);
        var byId = matches.ToDictionary(match => match.Id);

        return pageRows.Select(row =>
        {
            var match = byId[row.MatchId];
            var opponent = match.Players
                .Where(side => side.PlayerId != playerId)
                .OrderByDescending(side => side.PlayerId != null)
                .FirstOrDefault();
            var edition = match.TournamentEdition;
            return new HistoryRow(
                match.Id,
                match.SourceYear,
                match.PlayedOn,
                edition?.StartDate,
                match.WinnerCode,
                row.IsWinner,
                edition?.Tournament.Name ?? edition?.Name,
                edition?.Surface,
                edition?.Level,
                TennisDataRowMapper.ReadRound(match.RawRecord),
                TennisDataRowMapper.ReadScore(match.RawRecord),
                opponent?.PlayerId,
                opponent?.Player?.Name);
        }).ToArray();
    }

    private sealed record HistoryRow(
        Guid Id,
        int SourceYear,
        DateOnly? PlayedOn,
        DateOnly? TournamentStart,
        short? WinnerCode,
        bool? IsWinner,
        string? TournamentName,
        string? Surface,
        string? Level,
        string? Round,
        string? Score,
        Guid? OpponentId,
        string? OpponentName);
}
