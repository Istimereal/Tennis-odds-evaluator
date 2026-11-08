using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Models;

namespace Tennis.Api.Services;

public class PlayerService
{
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
            query = query.Where(player => player.Name.Contains(name));
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

        var recentRows = await _db.MatchPlayers
            .AsNoTracking()
            .Where(row => row.PlayerId == id)
            .OrderByDescending(row => row.Match.PlayedOn)
            .ThenByDescending(row => row.Match.SourceYear)
            .Take(10)
            .Select(row => new
            {
                row.Match.Id,
                row.Match.SourceYear,
                MatchDate = row.Match.PlayedOn,
                TournamentStart = row.Match.TournamentEdition != null ? row.Match.TournamentEdition.StartDate : null,
                row.Match.WinnerCode,
                row.IsWinner,
                TournamentName = row.Match.TournamentEdition != null ? row.Match.TournamentEdition.Tournament.Name : null,
                Surface = row.Match.TournamentEdition != null ? row.Match.TournamentEdition.Surface : null,
                Level = row.Match.TournamentEdition != null ? row.Match.TournamentEdition.Level : null
            })
            .ToListAsync(cancellationToken);
        var recent = recentRows
            .Select(row => new RecentMatchResponse(
                row.Id,
                row.SourceYear,
                row.MatchDate,
                row.TournamentStart,
                row.WinnerCode,
                row.IsWinner,
                row.TournamentName,
                row.Surface,
                row.Level,
                null))
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
}
